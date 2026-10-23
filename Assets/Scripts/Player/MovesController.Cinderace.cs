using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private readonly NetworkVariable<byte> cinderAttackCharge = new NetworkVariable<byte>();
    private readonly Dictionary<int, CinderRequest> cinderRequests = new Dictionary<int, CinderRequest>();
    private readonly Dictionary<ulong, CinderTargetState> cinderTargets = new Dictionary<ulong, CinderTargetState>();
    private readonly double[] cinderNextCast = new double[3];
    private bool cinderBound, cinderFlameSlow;
    private int cinderCastSerial, cinderGeneration, cinderHealingAttacks;
    private double cinderNextBasic, cinderBlazeReady, cinderActionUntil;
    private double CinderNow => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    private bool IsCinderace => pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.CinderPassive;
    public int CinderaceAttackCharge => cinderAttackCharge.Value;
    public int NextCinderaceCast() => ++cinderCastSerial;

    private sealed class CinderRequest
    {
        public CinderCast Kind;
        public int Generation;
        public bool Cancelled, Started;
        public CinderMoveObject Instance;
    }
    private sealed class CinderTargetState
    {
        public Pokemon Target;
        public readonly CinderStackState Stacks = new CinderStackState();
        public readonly ushort MarkId = CinderEffectIds.Acquire(), BurnId = CinderEffectIds.Acquire();
        public int BurnTicks;
        public float NextBurn, ReleaseAfter;
        public bool MarkVisual;
    }

    private void UpdateCinderaceRuntime()
    {
        if (!IsServer || !IsCinderace) return;
        if (!cinderBound)
        {
            cinderBound = true; pokemon.OnDeath += OnCinderaceDeath; pokemon.OnDamageDealt += OnCinderaceDamage;
        }
        if (pokemon.CurrentHp <= 0) return;
        if (CinderNow >= cinderBlazeReady && pokemon.CurrentHp <= pokemon.GetMaxHp() / 2f)
        {
            cinderBlazeReady = CinderNow + 30;
            CinderBuff(CinderMoveRules.BlazeCrit, Stat.CritRate, 10, 5, false);
            CinderBuff(CinderMoveRules.BlazeSpeed, Stat.AtkSpeed, 20, 5, false);
            SpawnCinderObject(CinderCast.Blaze, 0, transform.forward, pokemon, false, false, false);
        }
        var remove = new List<ulong>();
        foreach (var pair in cinderTargets)
        {
            var state = pair.Value; Pokemon target = state.Target;
            if (target == null || !target.IsSpawned || target.CurrentHp <= 0) { remove.Add(pair.Key); continue; }
            if (!target.HasStatusEffect(state.MarkId) || state.Stacks.Get(Time.time) == 0) state.Stacks.Clear();
            if (state.BurnTicks > 0)
            {
                if (!target.HasStatusEffect(state.BurnId)) state.BurnTicks = 0;
                else if (Time.time >= state.NextBurn)
                {
                    state.NextBurn += 1; state.BurnTicks--;
                    // Periodic and flare damage never generate cinders or item basic procs.
                    target.TakeDamageRPC(new DamageInfo(NetworkObjectId, 0.2f, 0, 0, DamageType.Physical, DamageProprieties.Unused_2));
                    if (state.BurnTicks == 0) target.RemoveStatusEffectWithID(state.BurnId);
                }
            }
            if (state.Stacks.Get(Time.time) == 0 && state.BurnTicks == 0 && Time.time > state.ReleaseAfter) remove.Add(pair.Key);
        }
        foreach (ulong id in remove) ReleaseCinderTarget(id);
    }

    private CinderTargetState CinderTarget(Pokemon target)
    {
        if (!cinderTargets.TryGetValue(target.NetworkObjectId, out var state))
        {
            state = new CinderTargetState { Target = target }; cinderTargets.Add(target.NetworkObjectId, state);
        }
        return state;
    }
    public int GetCinderaceMarks(Pokemon target) => target != null && cinderTargets.TryGetValue(target.NetworkObjectId, out var state)
        && target.HasStatusEffect(state.MarkId) ? state.Stacks.Get(Time.time) : 0;
    public bool HasCinderaceBurn(Pokemon target) => target != null && cinderTargets.TryGetValue(target.NetworkObjectId, out var state)
        && state.BurnTicks > 0 && target.HasStatusEffect(state.BurnId);

    private void OnCinderaceDamage(ulong targetId, DamageInfo damage)
    {
        if (!IsServer || !IsCinderace || pokemon.CurrentHp <= 0 || (damage.proprieties & (DamageProprieties.Unused_2 | DamageProprieties.IsMuscleBand | DamageProprieties.IsHeal)) != 0
            || (damage.proprieties & (DamageProprieties.IsBasicAttack | DamageProprieties.Unused_1)) == 0
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj) || !obj.TryGetComponent(out Pokemon target)
            || !CinderMoveRules.CanHit(playerManager, target)) return;
        var state = CinderTarget(target);
        if (!target.HasStatusEffect(state.MarkId)) state.Stacks.Clear();
        bool flare = state.Stacks.Add((damage.proprieties & DamageProprieties.Unused_1) != 0 ? 2 : 1, Time.time);
        state.ReleaseAfter = Time.time + 5.1f;
        target.RemoveStatusEffectWithID(state.MarkId);
        if (flare)
        {
            int amount = CinderMoveRules.FlareDamage(pokemon.GetAttack(), pokemon.CurrentLevel, target.GetMaxHp(), target.Type != PokemonType.Player);
            target.TakeDamageRPC(new DamageInfo(NetworkObjectId, 0, 0, (short)Mathf.Min(short.MaxValue, amount), DamageType.Physical, DamageProprieties.Unused_2));
        }
        else
        {
            target.AddStatusEffect(new StatusEffect(StatusType.Scriptable, 5, true, state.MarkId));
            if (!state.MarkVisual)
            {
                state.MarkVisual = true;
                SpawnCinderObject(CinderCast.Cinders, 0, transform.forward, target, false, false, false);
            }
        }
    }
    public void CinderaceMarksVisualEnded(Pokemon target)
    {
        if (target != null && cinderTargets.TryGetValue(target.NetworkObjectId, out var state)) state.MarkVisual = false;
    }

    public void ApplyCinderaceBurn(Pokemon target)
    {
        if (!IsServer || !CinderMoveRules.CanHit(playerManager, target)) return;
        var state = CinderTarget(target);
        bool fresh = state.BurnTicks == 0 || !target.HasStatusEffect(state.BurnId);
        state.BurnTicks = 3; state.NextBurn = Time.time + 1; state.ReleaseAfter = Time.time + 5.1f;
        target.RemoveStatusEffectWithID(state.BurnId);
        target.AddStatusEffect(new StatusEffect(StatusType.Burned, 3.1f, true, state.BurnId));
        target.RemoveAllStatChangeWithIDRPC(state.BurnId);
        target.AddStatChange(new StatChange(5, Stat.Attack, 2, true, false, true, state.BurnId));
        target.AddStatChange(new StatChange(5, Stat.SpAttack, 2, true, false, true, state.BurnId));
        if (fresh) SpawnCinderObject(CinderCast.Burn, 0, transform.forward, target, false, false, false);
    }
    public void CinderBuff(ushort id, Stat stat, short amount, float duration, bool percentage)
    {
        pokemon.RemoveAllStatChangeWithIDRPC(id);
        pokemon.AddStatChange(new StatChange(amount, stat, duration, true, true, percentage, id));
    }
    public void CinderaceUnstoppable(ushort id, float duration)
    {
        // Use removal notifications before adding immunity. The legacy generic
        // Unstoppable path removes entries while iterating and omits the action
        // unlock notifications for roots/stuns.
        var negative = new List<StatusEffect>();
        foreach (StatusEffect effect in pokemon.StatusEffects) if (effect.IsNegativeStatus()) negative.Add(effect);
        foreach (StatusEffect effect in negative) pokemon.RemoveStatusEffectRPC(effect);
        pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, duration, true, id));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CastCinderaceRPC(CinderCast kind, int cast, Vector3 direction, ulong targetId)
    {
        bool allowed = IsCinderace && pokemon.CurrentHp > 0 && Enum.IsDefined(typeof(CinderCast), kind) && kind <= CinderCast.Unite && cast > 0 && CinderNow >= cinderActionUntil
            && !cinderRequests.ContainsKey(cast) && !pokemon.HasAnyStatusEffect(CinderMoveRules.Interrupts)
            && !(CinderMoveRules.Dash(kind) && pokemon.HasStatusEffect(StatusType.Immobilized))
            && float.IsFinite(direction.x) && float.IsFinite(direction.y) && float.IsFinite(direction.z);
        int slot = CinderMoveRules.Slot(kind);
        bool learned = false;
        if (allowed)
            foreach (LearnableMove entry in pokemon.BaseStats.LearnableMoves)
                if (entry.level <= pokemon.CurrentLevel && !entry.isUpgraded)
                    foreach (MoveAsset asset in entry.moves)
                        if (asset != null && asset.move == CinderMoveRules.Move(kind) && prefabLearnedMoves[slot] == asset.move) learned = true;
        Pokemon target = null;
        if (allowed && CinderMoveRules.Targeted(kind))
            allowed = NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj) && obj.TryGetComponent(out target)
                && CinderMoveRules.CanHit(playerManager, target) && !target.HasStatusEffect(StatusType.Invisible)
                && Vector3.Distance(transform.position, target.transform.position) <= CinderMoveRules.Range(kind) + 0.5f;
        if (!allowed || !learned || CinderNow + 0.1 < cinderNextCast[slot]) { AcceptCinderaceRPC(kind, cast, false); return; }
        foreach (var existing in cinderRequests.Values)
            if (!existing.Started && !existing.Cancelled) { AcceptCinderaceRPC(kind, cast, false); return; }
        cinderNextCast[slot] = CinderNow + Math.Max(0.25, CinderMoveRules.Cooldown(kind) * (1 - pokemon.GetCDR() / 100f));
        SpawnCinderObject(kind, cast, CinderMoveRules.Direction(direction, transform.forward), target, CinderMoveRules.Upgraded(kind, pokemon.CurrentLevel), false, false);
    }

    private void SpawnCinderObject(CinderCast kind, int cast, Vector3 direction, Pokemon target, bool upgraded, bool boosted, bool heal, bool slow = false)
    {
        var request = new CinderRequest { Kind = kind, Generation = cinderGeneration };
        if (cast != 0) cinderRequests.Add(cast, request);
        Addressables.LoadAssetAsync<GameObject>(CinderMoveRules.Path(kind)).Completed += handle =>
        {
            bool invalid = handle.Status != AsyncOperationStatus.Succeeded || request.Cancelled || this == null || !IsSpawned
                || request.Generation != cinderGeneration || !IsCinderace || pokemon.CurrentHp <= 0
                || (cast != 0 && pokemon.HasAnyStatusEffect(CinderMoveRules.Interrupts))
                || (CinderMoveRules.Targeted(kind) && !CinderMoveRules.CanHit(playerManager, target));
            if (invalid)
            {
                if (handle.Status != AsyncOperationStatus.Succeeded) Debug.LogError($"Failed loading Cinderace {kind}: {handle.OperationException}");
                Addressables.Release(handle);
                if (this != null && kind == CinderCast.Cinders) CinderaceMarksVisualEnded(target);
                if (this != null && IsSpawned && cast != 0 && !request.Cancelled)
                {
                    cinderNextCast[CinderMoveRules.Slot(kind)] = 0; AcceptCinderaceRPC(kind, cast, false);
                }
                if (this != null && cast != 0) cinderRequests.Remove(cast);
                return;
            }
            var go = Instantiate(handle.Result, transform.position, Quaternion.LookRotation(direction));
            var effect = go.GetComponent<CinderMoveObject>();
            if (effect == null)
            {
                Debug.LogError($"Cinderace {kind} prefab has no CinderMoveObject.");
                Destroy(go); Addressables.Release(handle);
                if (cast != 0) { cinderNextCast[CinderMoveRules.Slot(kind)] = 0; cinderRequests.Remove(cast); AcceptCinderaceRPC(kind, cast, false); }
                return;
            }
            request.Instance = effect; request.Started = true;
            effect.HoldAsset(handle); go.GetComponent<NetworkObject>().Spawn(true);
            if (cast != 0)
            {
                cinderAttackCharge.Value = 2;
                cinderActionUntil = CinderNow + (kind == CinderCast.BlazeKick ? 0.5 : CinderMoveRules.Dash(kind) ? 0.2 : CinderMoveRules.Recovery(kind));
                cinderNextCast[CinderMoveRules.Slot(kind)] = CinderNow + Math.Max(0.25, CinderMoveRules.Cooldown(kind) * (1 - pokemon.GetCDR() / 100f));
                AcceptCinderaceRPC(kind, cast, true);
            }
            effect.Initialize(this, playerManager, kind, cast, direction, target, upgraded, boosted, heal, slow);
        };
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void AcceptCinderaceRPC(CinderCast kind, int cast, bool success)
    {
        if (GetMove((MoveType)CinderMoveRules.Slot(kind)) is CinderMoveBase move && move.Kind == kind) move.Accept(cast, success);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CancelCinderaceCastRPC(int cast)
    {
        if (!cinderRequests.TryGetValue(cast, out var request)) return;
        request.Cancelled = true;
        if (!request.Started) cinderNextCast[CinderMoveRules.Slot(request.Kind)] = 0;
        if (request.Instance != null) request.Instance.Despawn();
        cinderRequests.Remove(cast);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CinderaceMovementPhaseRPC(int cast, bool finished, Vector3 position)
    {
        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)
            || Vector3.Distance(position, transform.position) > 3f) return;
        if (cinderRequests.TryGetValue(cast, out var request) && request.Instance != null) request.Instance.MovementPhase(finished, position);
    }
    public void EndCinderaceObject(int cast) { if (cast != 0) cinderRequests.Remove(cast); }
    public void ArmCinderaceFlameSlow() { cinderFlameSlow = true; }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CinderaceBasicRPC(ulong targetId)
    {
        if (!IsCinderace || pokemon.CurrentHp <= 0 || CinderNow < cinderActionUntil || pokemon.HasAnyStatusEffect(CinderMoveRules.Interrupts) || CinderNow + 0.06 < cinderNextBasic
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj) || !obj.TryGetComponent(out Pokemon target)
            || !CinderMoveRules.CanHit(playerManager, target) || Vector3.Distance(transform.position, target.transform.position) > 7.5f) return;
        bool boosted = cinderAttackCharge.Value == 2, heal = cinderHealingAttacks > 0;
        bool slow = boosted && cinderFlameSlow;
        cinderAttackCharge.Value = (byte)(boosted ? 0 : cinderAttackCharge.Value + 1);
        if (heal) cinderHealingAttacks--;
        if (slow) cinderFlameSlow = false;
        cinderNextBasic = CinderNow + GetAtkSpeedCooldown();
        SpawnCinderObject(CinderCast.Basic, 0, transform.forward, target, false, boosted, heal, slow);
    }
    public bool CinderaceHit(Pokemon target, DamageInfo damage, bool healing = false, bool slow = false)
    {
        if (!IsServer || !CinderMoveRules.CanHit(playerManager, target)) return false;
        int healthDamage = 0; bool applied = false;
        void Record(Pokemon victim, DamageInfo info, int amount)
        {
            if (victim == target && info.ratio == damage.ratio && info.baseDmg == damage.baseDmg && (info.proprieties & DamageProprieties.Unused_2) == 0)
            { applied = true; healthDamage += amount; }
        }
        pokemon.OnHealthDamageDealt += Record;
        try { target.TakeDamageRPC(damage); }
        finally { pokemon.OnHealthDamageDealt -= Record; }
        if (healing && pokemon.CurrentHp > 0 && healthDamage > 0) pokemon.HealDamageRPC(Mathf.FloorToInt(healthDamage * 0.3f));
        if (applied && slow && CinderMoveRules.CanHit(playerManager, target) && !target.HasStatusEffect(StatusType.Unstoppable))
        {
            target.RemoveAllStatChangeWithIDRPC(CinderMoveRules.FlameSlow);
            target.AddStatChange(new StatChange(30, Stat.Speed, 2, true, false, true, CinderMoveRules.FlameSlow));
        }
        return applied;
    }
    public void ApplyCinderaceFeint(bool upgraded)
    {
        var slows = new List<StatChange>();
        foreach (StatChange change in pokemon.StatChanges) if (change.AffectedStat == Stat.Speed && !change.IsBuff && change.CanBeRemoved) slows.Add(change);
        foreach (StatChange slow in slows) pokemon.RemoveStatChangeRPC(slow);
        pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, 0.73f, true, CinderMoveRules.FeintInvincible));
        CinderBuff(CinderMoveRules.FeintSpeed, Stat.Speed, 40, 2, true);
        if (upgraded) cinderHealingAttacks = 3;
    }
    public void ReduceCinderaceMobilityCooldown()
    {
        cinderNextCast[1] = Math.Max(CinderNow, cinderNextCast[1] - 4);
        ReduceCinderaceMobilityRPC();
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void ReduceCinderaceMobilityRPC() { ReduceMoveCooldown(MoveType.MoveB, 4); }

    private void OnCinderaceDeath(DamageInfo info)
    {
        cinderGeneration++;
        foreach (var request in new List<CinderRequest>(cinderRequests.Values))
        {
            if (!request.Started) cinderNextCast[CinderMoveRules.Slot(request.Kind)] = 0;
            request.Cancelled = true; if (request.Instance != null) request.Instance.Despawn();
        }
        cinderRequests.Clear();
        foreach (ulong id in new List<ulong>(cinderTargets.Keys)) ReleaseCinderTarget(id);
        cinderAttackCharge.Value = 0; cinderHealingAttacks = 0; cinderFlameSlow = false;
        cinderActionUntil = 0;
    }
    private void ReleaseCinderTarget(ulong id)
    {
        if (!cinderTargets.TryGetValue(id, out var state)) return;
        if (state.Target != null && state.Target.IsSpawned)
        {
            state.Target.RemoveStatusEffectWithID(state.MarkId); state.Target.RemoveStatusEffectWithID(state.BurnId);
            state.Target.RemoveAllStatChangeWithIDRPC(state.BurnId);
        }
        CinderEffectIds.Release(state.MarkId); CinderEffectIds.Release(state.BurnId); cinderTargets.Remove(id);
    }
    private void DisposeCinderaceRuntime()
    {
        if (IsOwner)
        {
            foreach (MoveBase move in moves) if (move is CinderMoveBase) move.ResetMove();
            if (uniteMove is CinderMoveBase unite) unite.ResetMove();
        }
        if (IsServer && cinderBound) OnCinderaceDeath(default);
        if (pokemon != null && cinderBound) { pokemon.OnDeath -= OnCinderaceDeath; pokemon.OnDamageDealt -= OnCinderaceDamage; }
        cinderBound = false;
    }
}
