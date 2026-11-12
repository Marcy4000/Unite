using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    private LucarioRuntime lucarioRuntime;
    private int lucarioRequestSerial;
    private readonly NetworkVariable<int> lucarioCharge = new NetworkVariable<int>();
    private readonly NetworkVariable<int> lucarioEnergy = new NetworkVariable<int>();
    private readonly NetworkVariable<double> lucarioSpinUntil = new NetworkVariable<double>();
    private readonly NetworkVariable<double> lucarioKickUntil = new NetworkVariable<double>();
    private readonly NetworkVariable<double> lucarioPassiveReady = new NetworkVariable<double>();
    private readonly NetworkVariable<double> lucarioBoneUntil = new NetworkVariable<double>();
    private readonly NetworkVariable<double> lucarioPunchBegan = new NetworkVariable<double>(-1);
    private readonly NetworkVariable<bool> lucarioAura = new NetworkVariable<bool>();
    private readonly Dictionary<ulong, LucarioMarkerVisual> lucarioMarkerViews = new Dictionary<ulong, LucarioMarkerVisual>();
    private double lucarioMarkerRefresh, displayedLucarioBone;
    private Vector3 lucarioPunchDirection;
    private bool lucarioPunchReleased, lucarioPunchCancelled;
    private bool IsLucario => pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.LucarioPassive;
    public LucarioRuntime LucarioCombat => IsServer && IsLucario ? lucarioRuntime ?? (lucarioRuntime = new LucarioRuntime(playerManager)) : null;
    public int NextLucarioRequest() => ++lucarioRequestSerial;
    public int LucarioCharge => lucarioCharge.Value;
    public float LucarioSpinRemaining => Remaining(lucarioSpinUntil.Value);
    public float LucarioKickRemaining => Remaining(lucarioKickUntil.Value);
    public float LucarioPassiveRemaining => Remaining(lucarioPassiveReady.Value);
    public bool LucarioAuraReady => lucarioAura.Value;
    public bool HasLucarioMove(AvailableMoves id) => prefabLearnedMoves[0] == id || prefabLearnedMoves[1] == id || prefabLearnedMoves[2] == id;
    private float Remaining(double until) => NetworkManager == null ? 0 : Mathf.Max(0, (float)(until - NetworkManager.ServerTime.Time));
    public void SyncLucarioState(int charge, double spin, double kick, double passive, bool aura)
    {
        if (!IsServer || !IsSpawned) return;
        lucarioCharge.Value = charge; lucarioSpinUntil.Value = spin; lucarioKickUntil.Value = kick;
        lucarioPassiveReady.Value = passive; lucarioAura.Value = aura;
    }
    private void SyncLucarioEnergy() { if (IsServer && IsSpawned && IsLucario) lucarioEnergy.Value = Mathf.FloorToInt(legacyItemEnergy); }
    private void UpdateLucarioRuntime()
    {
        if (!IsLucario) { if (lucarioRuntime != null) DisposeLucarioRuntime(); return; }
        if (IsServer)
        {
            LucarioCombat.Tick();
            if (NetworkManager.ServerTime.Time >= lucarioMarkerRefresh)
            { lucarioMarkerRefresh = NetworkManager.ServerTime.Time + 0.25; LucarioCombat.RefreshMarkerPresentation(); }
        }
        if (!IsOwner) return;
        if (uniteMoveMaxCharge > 0)
        {
            uniteMoveCharge = lucarioEnergy.Value;
            if (uniteMoveCharge >= uniteMoveMaxCharge) uniteMoveStatus.RemoveStatus(ActionStatusType.Cooldown);
            else if (!uniteMoveStatus.HasStatus(ActionStatusType.Cooldown)) uniteMoveStatus.AddStatus(ActionStatusType.Cooldown);
        }
        if (playerManager.HPBar != null)
        {
            bool show = lucarioPunchBegan.Value >= 0 && pokemon.CurrentHp > 0;
            playerManager.HPBar.ShowGenericGuage(show);
            if (show) playerManager.HPBar.UpdateGenericGuageValue(LucarioMoveRules.ChargeFraction((float)(NetworkManager.ServerTime.Time - lucarioPunchBegan.Value)), 1);
        }
        if (playerManager.IsLocalHuman && BattleUIManager.instance != null && displayedLucarioBone != lucarioBoneUntil.Value)
        { displayedLucarioBone = lucarioBoneUntil.Value; BattleUIManager.instance.ShowMoveSecondaryCooldown(1, Remaining(lucarioBoneUntil.Value)); }
    }
    private void PrepareLucarioInput(Vector3 direction)
    { lucarioPunchDirection = direction; lucarioPunchReleased = lucarioPunchCancelled = false; }
    private void BeginLucarioExecution(LucarioMove definition, int slot)
    {
        prefabNextCast[slot] = definition is LucarioPowerUpPunch ? double.PositiveInfinity
            : slot == 2 ? 0 : NetworkManager.ServerTime.Time + definition.PatchedCooldown * Mathf.Max(0.01f, 1 - pokemon.GetCDR() / 100f);
    }
    private void FinishLucarioExecution(int slot, AvailableMoves id, int request)
    {
        if (double.IsPositiveInfinity(prefabNextCast[slot])) prefabNextCast[slot] = NetworkManager.ServerTime.Time + LucarioMoveRules.PunchCancelCooldown;
        LucarioCompletedRPC(id, slot == 2 ? 0 : Remaining(prefabNextCast[slot]), request);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void LucarioAcceptedRPC(AvailableMoves id, bool recast, int request)
    {
        if (uniteMove is LucarioMove unite && unite.MoveId == id && unite.MatchesExecutionRequest(request)) unite.AcceptExecution();
        foreach (var move in moves)
            if (!recast && move is LucarioMove definition && definition.MoveId == id && definition.MatchesExecutionRequest(request)) definition.AcceptExecution();
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void LucarioCompletedRPC(AvailableMoves id, float remaining, int request)
    {
        if (uniteMove is LucarioMove unite && unite.MoveId == id && unite.MatchesExecutionRequest(request)) unite.CompleteExecution(0);
        foreach (var move in moves)
            if (move is LucarioMove definition && definition.MoveId == id && definition.MatchesExecutionRequest(request)) definition.CompleteExecution(remaining);
    }
    public void SetLucarioOwnerCooldown(LucarioMove move, float remaining)
    {
        if (!IsOwner) return;
        int slot = (int)GetMoveSlot(move); if (slot >= 2 || moveStatuses[slot] == null) return;
        moveStatuses[slot].Cooldown = Mathf.Max(0, remaining);
        if (remaining > 0) moveStatuses[slot].AddStatus(ActionStatusType.Cooldown); else moveStatuses[slot].RemoveStatus(ActionStatusType.Cooldown);
        UpdateMoveUI(slot);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void LucarioCooldownRPC(int slot, float remaining)
    { if (slot < 2 && moves[slot] is LucarioMove move) SetLucarioOwnerCooldown(move, remaining); }
    public void ResetLucarioFirstMove()
    {
        if (!IsServer) return;
        prefabNextCast[0] = NetworkManager.ServerTime.Time; LucarioCooldownRPC(0, 0);
    }
    public void ReduceLucarioFirstMove(float amount)
    {
        if (!IsServer || double.IsPositiveInfinity(prefabNextCast[0])) return;
        prefabNextCast[0] = System.Math.Max(NetworkManager.ServerTime.Time, prefabNextCast[0] - amount);
        LucarioCooldownRPC(0, Remaining(prefabNextCast[0]));
    }
    public void StartLucarioPunchCooldown(bool cancelled)
    {
        if (!IsServer) return;
        var definition = new LucarioPowerUpPunch { BalanceId = CharactersList.Instance?.GetMoveAsset(AvailableMoves.LucarioPowerUpPunch)?.BalanceId };
        prefabNextCast[0] = NetworkManager.ServerTime.Time + (cancelled ? LucarioMoveRules.PunchCancelCooldown : definition.PatchedCooldown * Mathf.Max(0.01f, 1 - pokemon.GetCDR() / 100f));
    }
    public void SetLucarioPunchBegan(double began) { if (IsServer) lucarioPunchBegan.Value = began; }
    public void SetLucarioBoneExpiry(double until) { if (IsServer) lucarioBoneUntil.Value = until; }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void LucarioPunchInputRPC(Vector3 direction, bool released, bool cancelled, int request)
    {
        if (!IsLucario || !CombatTargeting.Finite(direction) || !HasLucarioMove(AvailableMoves.LucarioPowerUpPunch)
            || request != prefabOwnerRequests[0] || lucarioPunchReleased) return;
        if (direction.sqrMagnitude > 0.001f) lucarioPunchDirection = LucarioMoveRules.Flat(direction).normalized;
        lucarioPunchReleased |= released; lucarioPunchCancelled |= cancelled;
    }
    public void LucarioPunchInput(out Vector3 direction, out bool released, out bool cancelled)
    { direction = lucarioPunchDirection; released = lucarioPunchReleased; cancelled = lucarioPunchCancelled; }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void ShowLucarioAttachedVisualRPC(string resource, string bone, float duration)
    {
        if (!IsClient) return;
        var visual = TimedCombatVisual.Spawn(LucarioMoveRules.Visuals + resource, transform.position, transform.forward, Vector3.one, duration);
        if (visual != null) visual.GetComponent<LucarioAttachedVisual>().Bind(playerManager, bone);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void ShowLucarioMarkerRPC(ulong targetId, double readyAt, float range)
    {
        if (!IsClient || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var root) || !root.TryGetComponent(out Pokemon target)) return;
        if (!lucarioMarkerViews.TryGetValue(targetId, out var view) || view == null)
        {
            var prefab = Resources.Load<GameObject>(LucarioMoveRules.Visuals + "ExtremeMarker"); if (prefab == null) return;
            view = Instantiate(prefab).GetComponent<LucarioMarkerVisual>(); lucarioMarkerViews[targetId] = view;
        }
        view.Bind(playerManager, target, readyAt, range);
    }
    private void DisposeLucarioRuntime()
    {
        lucarioRuntime?.Dispose(); lucarioRuntime = null;
        foreach (var view in lucarioMarkerViews.Values) if (view != null) Destroy(view.gameObject);
        lucarioMarkerViews.Clear();
    }
}
