using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private sealed class FlygonRequest
    {
        public FlygonCast Kind;
        public bool Cancelled, Started;
        public FlygonMoveObject Effect;
        public Action<DamageInfo> OnDeath;
    }
    private readonly Dictionary<int, FlygonRequest> flygonRequests = new Dictionary<int, FlygonRequest>();
    private readonly double[] flygonNextCast = new double[3];
    private int flygonSerial, flygonBufferedSlot = -1;
    private bool flygonBufferedRelease;
    private float flygonBufferUntil;
    private double flygonActionUntil;
    private double FlygonNow => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    private bool IsLivingFlygon => pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.FlygonPassive && pokemon.CurrentHp > 0;
    public int NextFlygonCast() => ++flygonSerial;

    public bool CanRecastFlygon(int slot)
    {
        var move = slot == 2 ? uniteMove : moves[slot];
        var status = slot == 2 ? uniteMoveStatus : moveStatuses[slot];
        return move is FlygonMoveBase flygon && flygon.CanRecast && status != null
            && (status.GetStatus() & ~ActionStatusType.Cooldown) == ActionStatusType.None;
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CastFlygonRPC(FlygonCast kind, int cast, Vector3 direction, Vector3 destination)
    {
        if (!Enum.IsDefined(typeof(FlygonCast), kind) || cast <= 0 || !IsLivingFlygon || !FlygonMoveRules.Finite(direction)
            || !FlygonMoveRules.Finite(destination) || flygonRequests.ContainsKey(cast) || FlygonNow < flygonActionUntil
            || pokemon.HasAnyStatusEffect(FlygonMoveRules.Interrupts)
            || (kind == FlygonCast.Earthquake && pokemon.HasStatusEffect(StatusType.Immobilized)))
        { AcceptFlygonRPC(kind, cast, false); return; }
        int slot = FlygonMoveRules.Slot(kind);
        if (prefabLearnedMoves[slot] != FlygonMoveRules.Move(kind) || FlygonNow + 0.05 < flygonNextCast[slot])
        { AcceptFlygonRPC(kind, cast, false); return; }
        foreach (var existing in flygonRequests.Values)
            if (!existing.Cancelled && (!existing.Started || (FlygonMoveRules.Mode(existing.Kind) && existing.Effect != null && existing.Effect.Phase < 5))) { AcceptFlygonRPC(kind, cast, false); return; }
        var request = new FlygonRequest { Kind = kind };
        request.OnDeath = _ => CancelFlygonRequest(cast);
        flygonRequests.Add(cast, request);
        pokemon.OnDeath += request.OnDeath;
        Vector3 aim = FlygonMoveRules.Direction(direction, transform.forward);
        Vector3 point = FlygonMoveRules.Destination(transform.position, destination, FlygonMoveRules.Range(kind));
        bool upgraded = FlygonMoveRules.Upgraded(kind, pokemon.CurrentLevel);
        Addressables.LoadAssetAsync<GameObject>(FlygonMoveRules.Path(kind)).Completed += handle =>
        {
            bool invalid = handle.Status != AsyncOperationStatus.Succeeded || request.Cancelled || this == null || !IsSpawned || !IsLivingFlygon
                || pokemon.HasAnyStatusEffect(FlygonMoveRules.Interrupts)
                || prefabLearnedMoves[slot] != FlygonMoveRules.Move(kind);
            if (invalid)
            {
                if (handle.Status != AsyncOperationStatus.Succeeded) Debug.LogError($"Failed loading Flygon {kind}: {handle.OperationException}");
                Addressables.Release(handle);
                if (this != null) { ReleaseFlygonRequest(cast); if (IsSpawned && !request.Cancelled) AcceptFlygonRPC(kind, cast, false); }
                return;
            }
            var go = Instantiate(handle.Result, transform.position, Quaternion.LookRotation(aim));
            var effect = go.GetComponent<FlygonMoveObject>();
            if (effect == null)
            {
                Debug.LogError($"Flygon {kind} prefab has no FlygonMoveObject.");
                Destroy(go); Addressables.Release(handle); ReleaseFlygonRequest(cast); AcceptFlygonRPC(kind, cast, false); return;
            }
            request.Started = true; request.Effect = effect;
            effect.HoldAsset(handle); go.GetComponent<NetworkObject>().Spawn(true);
            double recovery = FlygonMoveRules.Mode(kind) ? kind == FlygonCast.Dig ? FlygonMoveRules.DigEnter : FlygonMoveRules.UniteFormation : FlygonMoveRules.Recovery(kind);
            flygonActionUntil = FlygonNow + recovery;
            flygonNextCast[slot] = FlygonNow + (slot == 2 ? FlygonMoveRules.UniteDuration + FlygonMoveRules.UniteFormation + 1
                : FlygonMoveRules.Cooldown(kind) * Mathf.Max(0.01f, 1 - pokemon.GetCDR() / 100f));
            AcceptFlygonRPC(kind, cast, true);
            effect.Initialize(this, playerManager, cast, aim, point, upgraded);
        };
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void AcceptFlygonRPC(FlygonCast kind, int cast, bool accepted)
    {
        if (!Enum.IsDefined(typeof(FlygonCast), kind)) return;
        if (GetMove((MoveType)FlygonMoveRules.Slot(kind)) is FlygonMoveBase move && move.Kind == kind) move.Accept(cast, accepted);
    }
    public void NotifyFlygonPhase(FlygonCast kind, int cast, byte phase) { if (IsServer && IsSpawned && NetworkManager != null && NetworkManager.IsListening) FlygonPhaseRPC(kind, cast, phase); }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void FlygonPhaseRPC(FlygonCast kind, int cast, byte phase)
    {
        if (GetMove((MoveType)FlygonMoveRules.Slot(kind)) is FlygonMoveBase move && move.Kind == kind) move.SetPhase(cast, phase);
    }
    public void FlygonEffectEnded(FlygonCast kind, int cast)
    {
        ReleaseFlygonRequest(cast);
        if (IsSpawned && NetworkManager != null && NetworkManager.IsListening) EndFlygonCastRPC(kind, cast);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void EndFlygonCastRPC(FlygonCast kind, int cast)
    {
        if (GetMove((MoveType)FlygonMoveRules.Slot(kind)) is FlygonMoveBase move && move.Kind == kind) move.End(cast);
    }
    private void ReleaseFlygonRequest(int cast)
    {
        if (!flygonRequests.TryGetValue(cast, out var request)) return;
        if (pokemon != null) pokemon.OnDeath -= request.OnDeath;
        flygonRequests.Remove(cast);
    }
    private void CancelFlygonRequest(int cast)
    {
        if (!flygonRequests.TryGetValue(cast, out var request)) return;
        request.Cancelled = true;
        if (request.Effect != null) request.Effect.Despawn();
        ReleaseFlygonRequest(cast);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CancelFlygonCastRPC(int cast) { CancelFlygonRequest(cast); }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void EndFlygonModeRPC(int cast)
    {
        if (flygonRequests.TryGetValue(cast, out var request) && request.Effect != null && FlygonMoveRules.Mode(request.Kind)) request.Effect.RequestEnd();
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void FlygonLandingRPC(int cast, Vector3 point)
    {
        if (!FlygonMoveRules.Finite(point) || (point - transform.position).sqrMagnitude > 25) return;
        if (flygonRequests.TryGetValue(cast, out var request) && request.Effect is EarthquakeArea quake) quake.Land(point);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void FlygonDigExitRPC(int cast, Vector3 point)
    {
        if (!FlygonMoveRules.Finite(point)) return;
        if (flygonRequests.TryGetValue(cast, out var request) && request.Effect is FlygonDigWarning dig) dig.SetExit(point);
    }
    private void BufferFlygonInput(int slot, bool released)
    {
        if (slot < 0 || slot >= 2 || !(moves[slot] is FlygonMoveBase) || moveStatuses[slot].HasStatus(ActionStatusType.Cooldown)
            || pokemon.HasAnyStatusEffect(FlygonMoveRules.Interrupts)) return;
        bool nearRecovery = false;
        foreach (var move in moves) if (move is FlygonMoveBase flygon && flygon.CanBuffer) nearRecovery = true;
        if (!nearRecovery) return;
        if (flygonBufferedSlot != slot) flygonBufferedRelease = false;
        flygonBufferedSlot = slot; flygonBufferedRelease |= released; flygonBufferUntil = Time.time + FlygonMoveRules.InputBuffer;
    }
    private void ProcessFlygonBufferedInput()
    {
        if (flygonBufferedSlot < 0) return;
        if (Time.time > flygonBufferUntil || !IsLivingFlygon || pokemon.HasAnyStatusEffect(FlygonMoveRules.Interrupts)) { ClearFlygonBufferedInput(); return; }
        int slot = flygonBufferedSlot;
        if (!moveStatuses[slot].HasStatus(ActionStatusType.None)) return;
        bool release = flygonBufferedRelease; ClearFlygonBufferedInput();
        moves[slot].Start(playerManager);
        if (release) moves[slot].Finish();
    }
    private void ClearFlygonBufferedInput() { flygonBufferedSlot = -1; flygonBufferedRelease = false; }
    private void DisposeFlygonRuntime()
    {
        ClearFlygonBufferedInput();
        if (IsOwner)
        {
            foreach (var move in moves) if (move is FlygonMoveBase) move.ResetMove();
            if (uniteMove is FlygonMoveBase) uniteMove.ResetMove();
        }
        if (IsServer) foreach (int id in new List<int>(flygonRequests.Keys)) CancelFlygonRequest(id);
    }
}
