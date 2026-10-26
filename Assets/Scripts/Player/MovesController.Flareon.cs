using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private readonly FlareonComboState flareonCombo = new FlareonComboState();
    private readonly NetworkVariable<byte> flareonAttackCharge = new NetworkVariable<byte>(readPerm: NetworkVariableReadPermission.Owner);
    private readonly NetworkVariable<byte> flareonStacks = new NetworkVariable<byte>();
    private readonly NetworkVariable<double> flareonStacksExpire = new NetworkVariable<double>();
    private readonly NetworkVariable<ulong> flareonRecastTarget = new NetworkVariable<ulong>(ulong.MaxValue);
    private readonly NetworkVariable<double> flareonRecastUntil = new NetworkVariable<double>();
    private readonly Dictionary<int, FlareonRequest> flareonRequests = new Dictionary<int, FlareonRequest>();
    private readonly double[] flareonNextCast = new double[3];
    private bool flareonBound, flareonStateLoading, flareonBufferedRelease;
    private int flareonSerial, flareonLastCast, flareonGeneration, flareonBufferedSlot = -1;
    private double flareonNextBasic, flareonActionUntil, flareonStateRetry;
    private float flareonBufferUntil;
    private double flareonShownRecast;
    private FlareonMoveObject flareonStateView;
    private double FlareonNow => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    private bool IsFlareon => pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.FlareonPassive;
    public int FlareonAttackCharge => flareonAttackCharge.Value;
    public int FlareonAttackStacks => flareonStacks.Value;
    public float FlareonStackRemaining => (float)Math.Max(0, flareonStacksExpire.Value - FlareonNow);
    public Pokemon FlareonRecastTarget => NetworkManager != null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(flareonRecastTarget.Value, out var obj) ? obj.GetComponent<Pokemon>() : null;
    public bool FlareonRecastAvailable => flareonRecastUntil.Value > FlareonNow && FlareonMoveRules.CanHit(playerManager, FlareonRecastTarget);
    public int NextFlareonCast() => ++flareonSerial;
    private sealed class FlareonRequest
    {
        public FlareonCast Kind;
        public int Generation;
        public bool Cancelled, Started;
        public FlareonMoveObject Effect;
    }
    private void UpdateFlareonRuntime()
    {
        if (!IsFlareon) return;
        if (IsOwner && flareonShownRecast != flareonRecastUntil.Value)
        {
            flareonShownRecast = flareonRecastUntil.Value;
            if (BattleUIManager.instance != null) BattleUIManager.instance.ShowMoveSecondaryCooldown(0, (float)Math.Max(0, flareonShownRecast - FlareonNow));
        }
        if (!IsServer) return;
        if (!flareonBound)
        {
            flareonBound = true; pokemon.OnDeath += OnFlareonDeath; pokemon.OnHpChange += OnFlareonHpChange; pokemon.OnDamageDealt += OnFlareonDamage;
        }
        if (pokemon.CurrentHp <= 0) return;
        flareonCombo.Tick(FlareonNow); SyncFlareonCombo();
        if (pokemon.CurrentLevel >= 3 && pokemon.HasStatChangeWithID(FlareonMoveRules.EeveeBuff)) pokemon.RemoveAllStatChangeWithIDRPC(FlareonMoveRules.EeveeBuff);
        if (flareonRecastUntil.Value > 0 && (!FlareonRecastAvailable || pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts))) ClearFlareonRecast();
        if (flareonStateView == null && !flareonStateLoading && FlareonNow >= flareonStateRetry) SpawnFlareonState();
    }
    private void SyncFlareonCombo()
    {
        if (flareonStacks.Value != flareonCombo.Stacks)
        {
            pokemon.RemoveAllStatChangeWithIDRPC(FlareonMoveRules.AttackBuff);
            if (flareonCombo.Stacks > 0) pokemon.AddStatChange(new StatChange((short)(flareonCombo.Stacks * 5), Stat.Attack, 0, false, true, true, FlareonMoveRules.AttackBuff));
        }
        flareonAttackCharge.Value = (byte)flareonCombo.AttackCharge;
        flareonStacks.Value = (byte)flareonCombo.Stacks; flareonStacksExpire.Value = flareonCombo.StacksExpire;
    }
    private void OnFlareonHpChange()
    {
        if (!IsServer || pokemon.CurrentHp <= 0 || pokemon.CurrentLevel >= 3 || pokemon.CurrentHp >= pokemon.GetMaxHp() * 0.5f
            || pokemon.HasStatChangeWithID(FlareonMoveRules.EeveeBuff)) return;
        pokemon.AddStatChange(new StatChange(10, Stat.Attack, 5, true, true, true, FlareonMoveRules.EeveeBuff));
    }
    private void OnFlareonDamage(ulong victimId, DamageInfo damage)
    {
        if (!IsServer || pokemon.CurrentHp <= 0 || pokemon.CurrentLevel < 3 || (damage.proprieties & (DamageProprieties.Unused_2 | DamageProprieties.IsMuscleBand | DamageProprieties.IsHeal)) != 0
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(victimId, out var obj) || !obj.TryGetComponent(out Pokemon target)
            || target.Type != PokemonType.Player || !target.HasStatusEffect(StatusType.Burned)) return;
        flareonCombo.AddStack(FlareonNow); SyncFlareonCombo();
    }
    private void OnFlareonDeath(DamageInfo damage)
    {
        flareonGeneration++; flareonCombo.Clear(); SyncFlareonCombo(); ClearFlareonRecast();
        flareonActionUntil = flareonNextBasic = 0;
        foreach (int id in new List<int>(flareonRequests.Keys)) CancelFlareonRequest(id);
        if (flareonStateView != null) flareonStateView.Despawn();
        ClearFlareonBufferedInput();
    }
    public void ArmFlareonRecast(Pokemon target, float duration)
    {
        if (!IsServer || !FlareonMoveRules.CanHit(playerManager, target)) return;
        flareonRecastTarget.Value = target.NetworkObjectId; flareonRecastUntil.Value = FlareonNow + duration;
    }
    private void ClearFlareonRecast() { if (!IsServer) return; flareonRecastTarget.Value = ulong.MaxValue; flareonRecastUntil.Value = 0; }
    public bool HitFlareonTarget(Pokemon target, DamageInfo damage, float burnDuration = 0)
    {
        if (!IsServer || !FlareonMoveRules.CanHit(playerManager, target)) return false;
        bool applied = false;
        void Record(Pokemon victim, DamageInfo info, int amount)
        {
            if (victim != target || info.attackerId != damage.attackerId || info.ratio != damage.ratio || info.baseDmg != damage.baseDmg) return;
            applied = true;
            // The damage was accepted, but the source's on-damage event has not fired yet.
            // Ignite here so blocked hits cannot burn and the opening hit consistently earns a passive stack.
            if (burnDuration > 0 && victim.CurrentHp > 0) FlareonMoveRules.Burn(victim, NetworkObjectId, burnDuration);
        }
        pokemon.OnHealthDamageDealt += Record;
        try { target.TakeDamageRPC(damage); } finally { pokemon.OnHealthDamageDealt -= Record; }
        return applied;
    }
    public void ReduceFlareonCooldown(MoveType slot, float seconds)
    {
        if (!IsServer || slot == MoveType.UniteMove || slot == MoveType.All) return;
        flareonNextCast[(int)slot] = Math.Max(FlareonNow, flareonNextCast[(int)slot] - seconds);
        ReduceFlareonCooldownRPC(slot, seconds);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void ReduceFlareonCooldownRPC(MoveType slot, float seconds) { ReduceMoveCooldown(slot, seconds); }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void FlareonBasicRPC(ulong targetId)
    {
        if (!IsFlareon || pokemon.CurrentHp <= 0 || FlareonNow < flareonActionUntil || FlareonNow + 0.04 < flareonNextBasic
            || pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts) || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)
            || !obj.TryGetComponent(out Pokemon primary) || !FlareonMoveRules.CanHit(playerManager, primary)
            || !CombatTargeting.WithinMeleeRange(transform.position, primary.transform.position, FlareonMoveRules.BasicRange)) return;
        flareonCombo.Tick(FlareonNow); SyncFlareonCombo();
        bool boosted = flareonCombo.AttackCharge == 2;
        Vector3 direction = FlareonMoveRules.Direction(primary.transform.position - transform.position, transform.forward);
        flareonNextBasic = FlareonNow + GetAtkSpeedCooldown();
        PresentFlareonBasicRPC(boosted, direction);
        if (pokemon.CurrentLevel < 3) playerManager.AnimationManager.PlayAnimation(boosted ? "ani_atk6_bat_0133" : "ani_atk4_bat_0133");
        StartCoroutine(ResolveFlareonBasic(primary, direction, boosted, flareonGeneration));
    }
    private IEnumerator ResolveFlareonBasic(Pokemon primary, Vector3 direction, bool boosted, int generation)
    {
        yield return new WaitForSeconds(0.1f);
        if (!IsFlareon || !IsSpawned || pokemon.CurrentHp <= 0 || generation != flareonGeneration || pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts)) yield break;
        bool connected = false;
        Vector3 origin = transform.position;
        var targets = CombatTargeting.SphereTargets(origin + direction * 1.35f + Vector3.up * 0.5f, 1.15f);
        foreach (Pokemon target in targets)
        {
            if (target == null || !CombatTargeting.WithinMeleeRange(origin, target.transform.position, FlareonMoveRules.BasicRange)) continue;
            if (!HitFlareonTarget(target, FlareonMoveRules.Damage(NetworkObjectId, FlareonCast.Basic, boosted: boosted))) continue;
            connected = true;
            if (boosted && pokemon.CurrentLevel >= 3) target.RefreshDamageOverTime(StatusType.Burned, NetworkObjectId, 2);
        }
        if (!connected) yield break;
        PresentFlareonContactRPC(boosted);
        flareonCombo.ConfirmBasic(FlareonNow); SyncFlareonCombo();
        if (boosted) { ReduceFlareonCooldown(MoveType.MoveA, 0.5f); ReduceFlareonCooldown(MoveType.MoveB, 0.5f); }
    }
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PresentFlareonBasicRPC(bool boosted, Vector3 direction)
    {
        if (!IsOwner) FlareonMoveObject.PresentBasic(NetworkObjectId, boosted, direction);
    }
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PresentFlareonContactRPC(bool boosted) { FlareonMoveObject.PresentContact(NetworkObjectId, boosted); }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CastFlareonRPC(FlareonCast kind, int cast, Vector3 direction, ulong targetId)
    {
        if (!Enum.IsDefined(typeof(FlareonCast), kind) || kind > FlareonCast.Slam || cast <= 0 || cast <= flareonLastCast
            || !IsFlareon || pokemon.CurrentHp <= 0 || !FlareonMoveRules.Finite(direction) || FlareonNow + 0.04 < flareonActionUntil
            || pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts) || (FlareonMoveRules.Driven(kind) && pokemon.HasStatusEffect(StatusType.Immobilized)))
        { AcceptFlareonRPC(kind, cast, false, Vector3.zero); return; }
        int slot = FlareonMoveRules.Slot(kind); bool slam = kind == FlareonCast.Slam;
        if (prefabLearnedMoves[slot] != FlareonMoveRules.Move(kind) || (!slam && FlareonNow + 0.04 < flareonNextCast[slot])
            || (slam && (!FlareonRecastAvailable || flareonRecastTarget.Value != targetId)))
        { AcceptFlareonRPC(kind, cast, false, Vector3.zero); return; }
        foreach (var existing in flareonRequests.Values)
            if (!existing.Started && !existing.Cancelled) { AcceptFlareonRPC(kind, cast, false, Vector3.zero); return; }
        Pokemon target = null;
        if (FlareonMoveRules.Targeted(kind) && (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj) || !obj.TryGetComponent(out target)
            || !FlareonMoveRules.CanHit(playerManager, target) || target.HasStatusEffect(StatusType.Invisible)
            || (target.transform.position - transform.position).sqrMagnitude > Mathf.Pow(FlareonMoveRules.Range(kind) + 0.4f, 2)))
        { AcceptFlareonRPC(kind, cast, false, Vector3.zero); return; }
        flareonLastCast = cast;
        var request = new FlareonRequest { Kind = kind, Generation = flareonGeneration }; flareonRequests.Add(cast, request);
        Vector3 aim = FlareonMoveRules.Direction(direction, transform.forward);
        Addressables.LoadAssetAsync<GameObject>(FlareonMoveRules.Path(kind)).Completed += handle =>
        {
            bool invalid = handle.Status != AsyncOperationStatus.Succeeded || this == null || !IsSpawned || request.Cancelled || request.Generation != flareonGeneration
                || !IsFlareon || pokemon.CurrentHp <= 0 || pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts)
                || (FlareonMoveRules.Driven(kind) && pokemon.HasStatusEffect(StatusType.Immobilized)) || prefabLearnedMoves[slot] != FlareonMoveRules.Move(kind)
                || (FlareonMoveRules.Targeted(kind) && (!FlareonMoveRules.CanHit(playerManager, target) || (target.transform.position - transform.position).sqrMagnitude > Mathf.Pow(FlareonMoveRules.Range(kind) + 0.4f, 2)))
                || (slam && (!FlareonRecastAvailable || flareonRecastTarget.Value != targetId));
            if (invalid)
            {
                if (handle.Status != AsyncOperationStatus.Succeeded) Debug.LogError($"Failed loading Flareon {kind}: {handle.OperationException}");
                Addressables.Release(handle);
                if (this != null) { flareonRequests.Remove(cast); if (IsSpawned && !request.Cancelled) AcceptFlareonRPC(kind, cast, false, Vector3.zero); }
                return;
            }
            Vector3 point = target != null ? target.transform.position - FlareonMoveRules.Direction(target.transform.position - transform.position, aim) * 0.65f : transform.position + aim * FlareonMoveRules.Range(kind);
            point.y = transform.position.y;
            var go = Instantiate(handle.Result, transform.position, Quaternion.LookRotation(aim));
            var effect = go.GetComponent<FlareonMoveObject>();
            if (effect == null) { Destroy(go); Addressables.Release(handle); flareonRequests.Remove(cast); AcceptFlareonRPC(kind, cast, false, Vector3.zero); return; }
            request.Started = true; request.Effect = effect;
            if (slam) ClearFlareonRecast();
            flareonActionUntil = FlareonNow + FlareonMoveRules.Recovery(kind);
            if (!slam) flareonNextCast[slot] = FlareonNow + (slot == 2 ? 1 : FlareonMoveRules.Cooldown(kind) * Math.Max(0.01, 1 - pokemon.GetCDR() / 100f));
            effect.HoldAsset(handle); go.GetComponent<NetworkObject>().Spawn(true);
            effect.Initialize(this, playerManager, kind, cast, aim, target, FlareonMoveRules.Upgraded(kind, pokemon.CurrentLevel));
            AcceptFlareonRPC(kind, cast, true, point);
        };
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void AcceptFlareonRPC(FlareonCast kind, int cast, bool accepted, Vector3 destination)
    {
        if (!Enum.IsDefined(typeof(FlareonCast), kind) || kind > FlareonCast.Slam) return;
        if (GetMove((MoveType)FlareonMoveRules.Slot(kind)) is FlareonMoveBase move) move.Accept(cast, accepted, destination);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void FlareonMovementRPC(int cast, bool finished, Vector3 position)
    {
        if (flareonRequests.TryGetValue(cast, out var request) && request.Effect != null && !request.Cancelled) request.Effect.ReportMovement(finished, position);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CancelFlareonMoveRPC(FlareonCast kind)
    {
        foreach (int id in new List<int>(flareonRequests.Keys))
            if (flareonRequests[id].Kind == kind || kind == FlareonCast.Takedown && flareonRequests[id].Kind == FlareonCast.Slam) CancelFlareonRequest(id);
        if (kind == FlareonCast.Takedown) ClearFlareonRecast();
    }
    private void CancelFlareonRequest(int id)
    {
        if (!flareonRequests.TryGetValue(id, out var request)) return;
        request.Cancelled = true; if (request.Effect != null) request.Effect.Despawn(); flareonRequests.Remove(id);
    }
    public void EndFlareonObject(int id) { if (id > 0) flareonRequests.Remove(id); }
    private void SpawnFlareonState()
    {
        flareonStateLoading = true; flareonStateRetry = FlareonNow + 2; int generation = flareonGeneration;
        Addressables.LoadAssetAsync<GameObject>(FlareonMoveRules.Path(FlareonCast.State)).Completed += handle =>
        {
            if (this != null) flareonStateLoading = false;
            if (handle.Status != AsyncOperationStatus.Succeeded || this == null || !IsSpawned || !IsFlareon || generation != flareonGeneration || pokemon.CurrentHp <= 0) { Addressables.Release(handle); return; }
            var go = Instantiate(handle.Result, transform.position, transform.rotation); flareonStateView = go.GetComponent<FlareonMoveObject>();
            if (flareonStateView == null) { Destroy(go); Addressables.Release(handle); return; }
            flareonStateView.HoldAsset(handle); go.GetComponent<NetworkObject>().Spawn(true); flareonStateView.Initialize(this, playerManager, FlareonCast.State, 0, transform.forward, null, false);
        };
    }
    private bool CanRecastFlareon(int slot) => slot == 0 && moves[slot] is FlareonMoveBase move && move.CanRecast
        && (moveStatuses[slot].GetStatus() & ~ActionStatusType.Cooldown) == ActionStatusType.None;
    private void BufferFlareonInput(int slot, bool released)
    {
        if (slot < 0 || slot > 1 || !(moves[slot] is FlareonMoveBase) || (moveStatuses[slot].HasStatus(ActionStatusType.Cooldown) && !(slot == 0 && FlareonRecastAvailable))
            || pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts)) return;
        bool near = uniteMove is FlareonMoveBase unite && unite.CanBuffer;
        foreach (var entry in moves) if (entry is FlareonMoveBase move && move.CanBuffer) near = true;
        if (!near) return;
        if (flareonBufferedSlot != slot) flareonBufferedRelease = false;
        flareonBufferedSlot = slot; flareonBufferedRelease |= released; flareonBufferUntil = Time.time + FlareonMoveRules.InputBuffer;
    }
    private void ProcessFlareonBufferedInput()
    {
        if (flareonBufferedSlot < 0) return;
        if (Time.time > flareonBufferUntil || !IsFlareon || pokemon.CurrentHp <= 0 || pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts)) { ClearFlareonBufferedInput(); return; }
        int slot = flareonBufferedSlot;
        if (!moveStatuses[slot].HasStatus(ActionStatusType.None) && !CanRecastFlareon(slot)) return;
        bool released = flareonBufferedRelease; ClearFlareonBufferedInput(); moves[slot].Start(playerManager); if (released) moves[slot].Finish();
    }
    private void ClearFlareonBufferedInput() { flareonBufferedSlot = -1; flareonBufferedRelease = false; }
    private void DisposeFlareonRuntime()
    {
        ClearFlareonBufferedInput();
        if (IsOwner)
        {
            foreach (MoveBase move in moves) if (move is FlareonMoveBase) move.ResetMove();
            if (uniteMove is FlareonMoveBase) uniteMove.ResetMove();
        }
        if (pokemon != null && flareonBound) { pokemon.OnDeath -= OnFlareonDeath; pokemon.OnHpChange -= OnFlareonHpChange; pokemon.OnDamageDealt -= OnFlareonDamage; }
        if (IsServer && flareonBound) OnFlareonDeath(default);
        flareonBound = false;
    }
}
