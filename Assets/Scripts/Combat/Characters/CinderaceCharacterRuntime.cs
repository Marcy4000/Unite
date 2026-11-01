using System;
using System.Collections;
using System.Collections.Generic;
using Unite.Combat;
using UnityEngine;
using UnityEngine.AddressableAssets;

public sealed class CinderaceCharacterRuntime : CombatCharacterRuntime
{
    [Serializable]
    public sealed class PresentationState
    {
        public long Revision;
        public byte Charge;
        public bool MovementLocked;
    }

    private sealed class CinderTargetState
    {
        public Pokemon Target;
        public readonly CinderStackState Stacks = new CinderStackState();
        public readonly ushort MarkId = CinderEffectIds.Acquire(), BurnId = CinderEffectIds.Acquire();
        public float ReleaseAfter;
        public bool MarkVisual;
        public CombatScope VisualScope;
    }

    private readonly CombatScope lifetime = new CombatScope();
    private readonly Dictionary<CinderCast, GameObject> prefabs = new Dictionary<CinderCast, GameObject>();
    private readonly HashSet<CinderCast> loading = new HashSet<CinderCast>();
    private Pokemon boundPokemon;
    private bool disposed;

    private byte charge;
    private CombatRoutine basicRecovery;
    private int shownCharge = -1;
    private CinderBasicAtk shownAttack;

    private readonly Dictionary<ulong, CinderTargetState> targets = new Dictionary<ulong, CinderTargetState>();
    private double blazeReady;

    private int healingAttacks;
    private bool flameSlow;

    private int movementLocks;
    private bool movementLocked, appliedMovement;
    private CombatRoutine dashRoutine;
    private Vector3 dashDestination;
    private bool dashActive;
    private Vector2 movementInput;
    private float nextMovementInputAt;
    private long stateRevision;

    public CinderaceCharacterRuntime(PlayerManager actor) : base(actor)
    {
        lifetime.Own(new CombatCallbackLease(() => basicRecovery?.Dispose()));
        lifetime.Own(new CombatCallbackLease(() => StopDashPresentation(false, Vector3.zero)));
        BindEvents();
    }

    private double Now => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    private bool IsCinderace => Pokemon != null && Pokemon.BaseStats != null && Pokemon.BaseStats.Passive == AvailablePassives.CinderPassive;

    public byte Charge => charge;
    public int HealingAttacks => healingAttacks;
    public bool FlameSlowArmed => flameSlow;
    public bool MovementLocked => movementLocked;
    public Vector2 MovementInput => movementInput;

    public override bool PrepareCast(CombatExecutionContext context)
    {
        if (disposed || !IsCinderace) return false;
        CinderCast kind = KindForMove(context.MoveId);
        if (CinderMoveRules.Dash(kind) && Pokemon.HasStatusEffect(StatusType.Immobilized)) return false;
        return true;
    }

    public override bool RevalidateCast(CombatExecutionContext context) => PrepareCast(context);

    public override void CommitCast(CombatExecutionContext context)
    {
        FillCharge();
    }

    public override CombatExecution CreateExecution(CombatExecutionContext context) => new CinderaceExecution(context, this);

    public static CinderCast KindForMove(AvailableMoves moveId)
    {
        switch (moveId)
        {
            case AvailableMoves.CinderEmber: return CinderCast.Ember;
            case AvailableMoves.CinderLowSweep: return CinderCast.LowSweep;
            case AvailableMoves.CinderPyroball: return CinderCast.PyroBall;
            case AvailableMoves.CinderBlazeKick: return CinderCast.BlazeKick;
            case AvailableMoves.CinderFlameCharge: return CinderCast.FlameCharge;
            case AvailableMoves.CinderFeint: return CinderCast.Feint;
            default: return CinderCast.Unite;
        }
    }

    public override void Tick()
    {
        if (disposed || !IsSpawned || Pokemon == null || !IsCinderace) return;
        if (IsServer)
        {
            BindEvents();
            EnsurePrefab(CinderCast.Basic);
            EnsurePersistentPassivePrefabs();
            if (Pokemon.CurrentHp <= 0)
            {
                ResetCombatCharges();
            }
            else TickBlazePassive();
            PruneCinderTargetState();
        }
        Present();
    }

    public void FillCharge()
    {
        if (!IsServer || disposed) return;
        charge = 2;
        Publish();
    }

    public void ResetBasicCharge()
    {
        if (!IsServer || disposed) return;
        basicRecovery?.Dispose();
        basicRecovery = null;
        charge = 0;
        Publish();
    }

    public void PerformBasicAttack(Pokemon target)
    {
        if (!IsServer || disposed || !IsCinderace || Pokemon.CurrentHp <= 0 || !CinderMoveRules.CanHit(Actor, target)
            || !CombatTargeting.WithinMeleeRange(transform.position, target.transform.position, 7f)) return;
        byte previous = charge;
        bool boosted = previous == 2;
        bool heal = ConsumeFeintHealingAttack();
        bool slow = boosted && ConsumeFlameChargeSlow();
        charge = (byte)(boosted ? 0 : previous + 1);
        Vector3 direction = CinderMoveRules.Direction(target.transform.position - transform.position, transform.forward);
        if (prefabs.TryGetValue(CinderCast.Basic, out GameObject cached) && cached != null)
            SpawnPreparedObject(cached, CinderCast.Basic, 0, direction, target, false, boosted, heal, slow, lifetime, false);
        else
            lifetime.Own(new CombatRoutine(Actor, LoadAndSpawnBasic(direction, target, boosted, heal, slow)));
        string animation = $"ani_atk{previous + 1}_bat_0815";
        basicRecovery?.Dispose();
        basicRecovery = new CombatRoutine(Actor, BasicRecovery(target, animation));
        Publish();
    }

    private IEnumerator LoadAndSpawnBasic(Vector3 direction, Pokemon target, bool boosted, bool heal, bool slow)
    {
        GameObject loadedPrefab = null;
        yield return Controller.Combat.Spawner.Load(CinderMoveRules.Path(CinderCast.Basic), lifetime, loaded => loadedPrefab = loaded);
        if (disposed || !IsServer || !IsSpawned || Pokemon == null || Pokemon.CurrentHp <= 0 || !CinderMoveRules.CanHit(Actor, target))
            yield break;
        if (loadedPrefab != null && loadedPrefab.GetComponent<CinderMoveObject>() != null)
        {
            prefabs[CinderCast.Basic] = loadedPrefab;
            SpawnPreparedObject(loadedPrefab, CinderCast.Basic, 0, direction, target, false, boosted, heal, slow, lifetime, false);
        }
    }

    private IEnumerator BasicRecovery(Pokemon target, string animation)
    {
        float duration = 0.5f * Controller.GetAtkSpeedCooldown();
        Vector3 direction = target != null ? target.transform.position - transform.position : transform.forward;
        Controller.PresentServerAttackRPC(true, direction, animation, duration);
        try { yield return new WaitForSeconds(duration); }
        finally
        {
            if (Actor != null && Actor.IsSpawned)
            {
                Controller.PresentServerAttackRPC(false, Vector3.zero, "");
                RecoverAnimation(animation);
            }
        }
    }

    internal void RecoverAnimation(string animation)
    {
        if (!IsServer || !IsSpawned || Pokemon == null || Pokemon.CurrentHp <= 0 || Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions)) return;
        Actor.AnimationManager?.RecoverAnimationRPC(animation, "ani_idle_bat_0815", 0.06f);
    }

    private void TickBlazePassive()
    {
        if (Now < blazeReady || Pokemon.CurrentHp > Pokemon.GetMaxHp() / 2f) return;
        blazeReady = Now + 30;
        ApplyBuff(CinderMoveRules.BlazeCrit, Stat.CritRate, 10, 5f, false);
        ApplyBuff(CinderMoveRules.BlazeSpeed, Stat.AtkSpeed, 20, 5f, false);
        SpawnAuxiliaryObject(CinderCast.Blaze, transform.forward, Pokemon, lifetime);
    }

    private CinderTargetState TargetState(Pokemon target)
    {
        if (!targets.TryGetValue(target.NetworkObjectId, out CinderTargetState state))
        {
            state = new CinderTargetState { Target = target };
            targets.Add(target.NetworkObjectId, state);
        }
        return state;
    }

    public int GetMarks(Pokemon target)
        => target != null && targets.TryGetValue(target.NetworkObjectId, out CinderTargetState state)
            && target.HasStatusEffect(state.MarkId) ? state.Stacks.Get(Time.time) : 0;

    public bool HasBurn(Pokemon target)
        => target != null && targets.TryGetValue(target.NetworkObjectId, out CinderTargetState state)
            && target.HasDamageOverTime(StatusType.Burned, Controller.NetworkObjectId);

    private void OnDamageDealt(ulong targetId, DamageInfo damage)
    {
        if (!IsServer || disposed || !IsCinderace || Pokemon == null || Pokemon.CurrentHp <= 0
            || (damage.proprieties & (DamageProprieties.Unused_2 | DamageProprieties.IsMuscleBand | DamageProprieties.IsHeal)) != 0
            || (damage.proprieties & (DamageProprieties.IsBasicAttack | DamageProprieties.Unused_1)) == 0
            || NetworkManager == null || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)
            || !obj.TryGetComponent(out Pokemon target) || !CinderMoveRules.CanHit(Actor, target)) return;
        CinderTargetState state = TargetState(target);
        if (!target.HasStatusEffect(state.MarkId)) state.Stacks.Clear();
        bool flare = state.Stacks.Add((damage.proprieties & DamageProprieties.Unused_1) != 0 ? 2 : 1, Time.time);
        state.ReleaseAfter = Time.time + 5.1f;
        target.RemoveStatusEffectWithID(state.MarkId);
        if (flare)
        {
            int amount = CinderMoveRules.FlareDamage(Pokemon.GetAttack(), Pokemon.CurrentLevel, target.GetMaxHp(), target.Type != PokemonType.Player);
            target.TakeDamageRPC(new DamageInfo(Controller.NetworkObjectId, 0f, 0, (short)Mathf.Min(short.MaxValue, amount), DamageType.Physical, DamageProprieties.Unused_2));
        }
        else
        {
            target.AddStatusEffect(new StatusEffect(StatusType.Scriptable, 5f, true, state.MarkId));
            if (!state.MarkVisual)
            {
                state.MarkVisual = true;
                state.VisualScope?.Dispose();
                state.VisualScope = new CombatScope();
                SpawnAuxiliaryObject(CinderCast.Cinders, transform.forward, target, state.VisualScope);
            }
        }
    }

    public void MarksVisualEnded(Pokemon target)
    {
        if (target != null && targets.TryGetValue(target.NetworkObjectId, out CinderTargetState state))
            state.MarkVisual = false;
    }

    public void ApplyBurn(Pokemon target)
    {
        if (!IsServer || disposed || !CinderMoveRules.CanHit(Actor, target)) return;
        CinderTargetState state = TargetState(target);
        state.ReleaseAfter = Time.time + 5.1f;
        target.AddStatusEffect(MovesController.CreateCinderaceBurnEffect(Controller.NetworkObjectId, state.BurnId));
        target.RemoveAllStatChangeWithIDRPC(state.BurnId);
        target.AddStatChange(new StatChange(5, Stat.Attack, 2f, true, false, true, state.BurnId));
        target.AddStatChange(new StatChange(5, Stat.SpAttack, 2f, true, false, true, state.BurnId));
    }

    private void PruneCinderTargetState()
    {
        var remove = new List<ulong>();
        foreach (var pair in targets)
        {
            CinderTargetState state = pair.Value;
            Pokemon target = state.Target;
            if (target == null || !target.IsSpawned || target.CurrentHp <= 0)
            {
                remove.Add(pair.Key);
                continue;
            }
            if (!target.HasStatusEffect(state.MarkId) || state.Stacks.Get(Time.time) == 0) state.Stacks.Clear();
            if (state.Stacks.Get(Time.time) == 0 && !target.HasDamageOverTime(StatusType.Burned, Controller.NetworkObjectId) && Time.time > state.ReleaseAfter)
                remove.Add(pair.Key);
        }
        foreach (ulong id in remove) ReleaseTarget(id);
    }

    private void ReleaseTarget(ulong id)
    {
        if (!targets.TryGetValue(id, out CinderTargetState state)) return;
        state.VisualScope?.Dispose();
        state.VisualScope = null;
        if (state.Target != null && state.Target.IsSpawned)
        {
            state.Target.RemoveStatusEffectWithID(state.MarkId);
            state.Target.RemoveStatusEffectWithID(state.BurnId);
            state.Target.RemoveAllStatChangeWithIDRPC(state.BurnId);
        }
        CinderEffectIds.Release(state.MarkId);
        CinderEffectIds.Release(state.BurnId);
        targets.Remove(id);
    }

    private void EnsurePersistentPassivePrefabs()
    {
        EnsurePrefab(CinderCast.Blaze);
        EnsurePrefab(CinderCast.Cinders);
    }

    public void ApplyFeint(bool upgraded)
    {
        if (!IsServer || disposed || Pokemon == null) return;
        var slows = new List<StatChange>();
        foreach (StatChange change in Pokemon.StatChanges)
            if (change.AffectedStat == Stat.Speed && !change.IsBuff && change.CanBeRemoved) slows.Add(change);
        foreach (StatChange slow in slows) Pokemon.RemoveStatChangeRPC(slow);
        Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, 0.73f, true, CinderMoveRules.FeintInvincible));
        ApplyBuff(CinderMoveRules.FeintSpeed, Stat.Speed, 40, 2f, true);
        if (upgraded) ArmFeintHealingAttacks();
    }

    private void ArmFeintHealingAttacks()
    {
        healingAttacks = 3;
    }

    private bool ConsumeFeintHealingAttack()
    {
        if (healingAttacks <= 0) return false;
        healingAttacks--;
        return true;
    }

    public void ArmFlameSlow()
    {
        if (!IsServer || disposed) return;
        SetFlameChargeSlowArmed(true);
    }

    private void SetFlameChargeSlowArmed(bool armed) => flameSlow = armed;

    private bool ConsumeFlameChargeSlow()
    {
        if (!flameSlow) return false;
        SetFlameChargeSlowArmed(false);
        return true;
    }

    public void ReduceMobilityCooldown()
    {
        if (!IsServer || disposed) return;
        Controller.Combat?.ReduceCooldown(1, 4d);
    }

    public void ApplyBuff(ushort id, Stat stat, short amount, float duration, bool percentage)
    {
        if (!IsServer || Pokemon == null) return;
        Pokemon.RemoveAllStatChangeWithIDRPC(id);
        Pokemon.AddStatChange(new StatChange(amount, stat, duration, true, true, percentage, id));
    }

    public void ApplyUnstoppable(ushort id, float duration)
    {
        if (!IsServer || Pokemon == null) return;
        var negative = new List<StatusEffect>();
        foreach (StatusEffect effect in Pokemon.StatusEffects) if (effect.IsNegativeStatus()) negative.Add(effect);
        foreach (StatusEffect effect in negative) Pokemon.RemoveStatusEffectRPC(effect);
        Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, duration, true, id));
    }

    public bool ResolveHit(Pokemon target, DamageInfo damage, bool healing = false, bool slow = false)
    {
        if (!IsServer || disposed || !CinderMoveRules.CanHit(Actor, target)) return false;
        BindEvents();
        int healthDamage = 0;
        bool applied = false;
        void Record(Pokemon victim, DamageInfo info, int amount)
        {
            if (victim == target && info.ratio == damage.ratio && info.baseDmg == damage.baseDmg && (info.proprieties & DamageProprieties.Unused_2) == 0)
            {
                applied = true;
                healthDamage += amount;
            }
        }
        Pokemon.OnHealthDamageDealt += Record;
        try { target.TakeDamageRPC(damage); }
        finally { Pokemon.OnHealthDamageDealt -= Record; }
        if (healing && Pokemon.CurrentHp > 0 && healthDamage > 0)
            Pokemon.HealDamageRPC(Mathf.FloorToInt(healthDamage * 0.3f));
        if (applied && slow && CinderMoveRules.CanHit(Actor, target) && !target.HasStatusEffect(StatusType.Unstoppable))
        {
            target.RemoveAllStatChangeWithIDRPC(CinderMoveRules.FlameSlow);
            target.AddStatChange(new StatChange(30, Stat.Speed, 2f, true, false, true, CinderMoveRules.FlameSlow));
        }
        return applied;
    }

    internal IDisposable AcquireMovementLock()
    {
        movementLocks++;
        movementLocked = true;
        Publish();
        return new CombatCallbackLease(() =>
        {
            movementLocked = --movementLocks > 0;
            Publish();
        });
    }

    public override bool HandleMovement(Vector2 input)
    {
        if (!IsCinderace || !IsSpawned || !IsOwner || Pokemon.CurrentHp <= 0) return false;
        if (Time.time >= nextMovementInputAt || (input - movementInput).sqrMagnitude > 0.01f)
        {
            nextMovementInputAt = Time.time + 0.05f;
            movementInput = Vector2.ClampMagnitude(input, 1f);
            Controller.CinderaceMovementRPC(movementInput);
        }
        return false;
    }

    public void ReceiveMovement(Vector2 input)
    {
        if (disposed || !IsCinderace || !float.IsFinite(input.x) || !float.IsFinite(input.y)) return;
        movementInput = Vector2.ClampMagnitude(input, 1f);
    }

    public Vector3 ReboundDirection(Vector3 fallback)
    {
        Vector2 input = Actor != null && (Actor.IsOwner || Actor.IsBot) ? Actor.MovementInput : movementInput;
        return CinderMoveRules.Direction(new Vector3(input.x, 0f, input.y), fallback);
    }

    internal void BeginDash(Vector3 origin, Vector3 destination, float duration)
    {
        if (IsServer && IsSpawned)
            Controller.CinderaceDashRPC(origin, destination, Now, duration, true, false);
    }

    internal void EndDash(bool completed)
    {
        if (IsServer && IsSpawned)
            Controller.CinderaceDashRPC(Vector3.zero, Vector3.zero, 0, 0, false, completed);
    }

    public void ReceiveDash(Vector3 origin, Vector3 destination, double started, float duration, bool active, bool completed)
    {
        if (disposed || (!IsOwner && !IsServer)) return;
        StopDashPresentation(completed, dashDestination);
        if (!active || duration <= 0f || !CombatTargeting.Finite(origin) || !CombatTargeting.Finite(destination)) return;
        dashDestination = destination;
        dashActive = true;
        dashRoutine = new CombatRoutine(Actor, DriveDash(origin, destination, started, duration));
    }

    private IEnumerator DriveDash(Vector3 origin, Vector3 destination, double started, float duration)
    {
        Vector3 direction = destination - origin;
        direction.y = 0f;
        float distance = direction.magnitude;
        Vector3 unit = distance > 0.001f ? direction / distance : transform.forward;
        float previousProgress = 0f;
        while (IsSpawned && Pokemon != null && Pokemon.CurrentHp > 0 && !Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions))
        {
            float progress = Mathf.Clamp01((float)((Now - started) / Math.Max(0.01, duration)));
            float step = distance * (progress - previousProgress);
            previousProgress = progress;
            if (step > 0f && Actor.PlayerMovement != null)
                Actor.PlayerMovement.CinderaceDashStep(unit * step);
            if (progress >= 1f) break;
            yield return null;
        }
        if (IsSpawned && Pokemon != null && Pokemon.CurrentHp > 0 && Actor.PlayerMovement != null)
            Actor.PlayerMovement.FinishCinderaceTraversal();
        dashActive = false;
    }

    private void StopDashPresentation(bool completed, Vector3 destination)
    {
        dashRoutine?.Dispose();
        dashRoutine = null;
        if (!dashActive) return;
        dashActive = false;
        if (!IsSpawned || Pokemon == null || Pokemon.CurrentHp <= 0 || Actor.PlayerMovement == null) return;
        if (completed && !Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions) && CombatTargeting.Finite(destination))
        {
            Vector3 remaining = destination - transform.position;
            remaining.y = 0f;
            if (remaining.sqrMagnitude > 0.0001f && remaining.sqrMagnitude <= 100f)
                Actor.PlayerMovement.CinderaceDashStep(remaining);
        }
        Actor.PlayerMovement.FinishCinderaceTraversal();
    }

    private void EnsurePrefab(CinderCast kind)
    {
        if (prefabs.ContainsKey(kind) || loading.Contains(kind)) return;
        lifetime.Own(new CombatRoutine(Actor, LoadPrefab(kind)));
    }

    private IEnumerator LoadPrefab(CinderCast kind)
    {
        loading.Add(kind);
        yield return Controller.Combat.Spawner.Load(CinderMoveRules.Path(kind), lifetime, loaded =>
        {
            if (loaded != null && loaded.GetComponent<CinderMoveObject>() != null && !disposed)
                prefabs[kind] = loaded;
        });
        loading.Remove(kind);
    }

    private void SpawnAuxiliaryObject(CinderCast kind, Vector3 direction, Pokemon target, CombatScope scope)
    {
        if (disposed || !IsServer || !IsSpawned || scope.IsDisposed) return;
        direction = CinderMoveRules.Direction(direction, transform.forward);
        if (prefabs.TryGetValue(kind, out GameObject cached) && cached != null)
        {
            SpawnPreparedObject(cached, kind, 0, direction, target, false, false, false, false, scope);
            return;
        }
        scope.Own(new CombatRoutine(Actor, LoadAndSpawnAuxiliary(kind, direction, target, scope)));
    }

    private IEnumerator LoadAndSpawnAuxiliary(CinderCast kind, Vector3 direction, Pokemon target, CombatScope scope)
    {
        GameObject loadedPrefab = null;
        yield return Controller.Combat.Spawner.Load(CinderMoveRules.Path(kind), scope, loaded => loadedPrefab = loaded);
        if (disposed || !IsServer || !IsSpawned || scope.IsDisposed || Pokemon == null || Pokemon.CurrentHp <= 0)
        {
            if (kind == CinderCast.Cinders) MarksVisualEnded(target);
            yield break;
        }
        if (loadedPrefab == null || loadedPrefab.GetComponent<CinderMoveObject>() == null)
        {
            if (kind == CinderCast.Cinders) MarksVisualEnded(target);
            yield break;
        }
        if (SpawnPreparedObject(loadedPrefab, kind, 0, direction, target, false, false, false, false, scope) == null && kind == CinderCast.Cinders)
            MarksVisualEnded(target);
    }

    internal CinderMoveObject SpawnPreparedObject(GameObject prefab, CinderCast kind, int cast, Vector3 direction, Pokemon target,
        bool upgraded, bool boosted, bool heal, bool slow, CombatScope scope, bool despawnWithScope = true)
    {
        if (disposed || !IsServer || !IsSpawned || prefab == null || scope.IsDisposed) return null;
        direction = CinderMoveRules.Direction(direction, transform.forward);
        GameObject spawned = Controller.Combat.Spawner.Spawn(prefab, transform.position, Quaternion.LookRotation(direction), scope, despawnWithScope);
        if (spawned == null) return null;
        var effect = spawned.GetComponent<CinderMoveObject>();
        if (effect == null) return null;
        effect.HoldAsset(Addressables.LoadAssetAsync<GameObject>(CinderMoveRules.Path(kind)));
        effect.Initialize(Controller, Actor, kind, cast, direction, target, upgraded, boosted, heal, slow);
        return effect;
    }

    private void Publish()
    {
        if (!disposed && IsServer && IsSpawned)
        {
            stateRevision++;
            PresentMovement();
            Controller.CinderaceSnapshotRPC(charge, movementLocked, stateRevision);
        }
    }

    public override string CapturePresentationState() => JsonUtility.ToJson(new PresentationState
    {
        Revision = stateRevision,
        Charge = charge,
        MovementLocked = movementLocked
    });

    public override void ApplyPresentationState(string state)
    {
        if (disposed || IsServer || string.IsNullOrEmpty(state)) return;
        PresentationState snapshot;
        try { snapshot = JsonUtility.FromJson<PresentationState>(state); }
        catch (ArgumentException) { return; }
        if (snapshot == null) return;
        ReceiveSnapshot(snapshot.Charge, snapshot.MovementLocked, snapshot.Revision);
    }

    public void ReceiveSnapshot(byte attackCharge, bool movement, long revision)
    {
        if (disposed || revision < stateRevision) return;
        stateRevision = revision;
        charge = (byte)Mathf.Clamp(attackCharge, 0, 2);
        movementLocked = movement;
        Present();
    }

    private void Present()
    {
        PresentMovement();
        if (IsSpawned && !IsOwner) return;
        var attack = Controller.BasicAttack as CinderBasicAtk;
        if (shownCharge != charge || shownAttack != attack)
        {
            shownCharge = charge;
            shownAttack = attack;
            attack?.ApplySnapshot(charge);
        }
    }

    private void PresentMovement()
    {
        if ((!IsOwner && !IsServer) || Actor.PlayerMovement == null) return;
        if (movementLocked != appliedMovement)
        {
            if (movementLocked) Actor.PlayerMovement.AddMovementRestriction();
            else Actor.PlayerMovement.RemoveMovementRestriction();
            appliedMovement = movementLocked;
        }
    }

    private void BindEvents()
    {
        if (boundPokemon == Pokemon) return;
        UnbindEvents();
        boundPokemon = Pokemon;
        if (boundPokemon != null)
        {
            boundPokemon.OnDeath += OnDeath;
            boundPokemon.OnDamageDealt += OnDamageDealt;
        }
    }

    private void UnbindEvents()
    {
        if (boundPokemon == null) return;
        boundPokemon.OnDeath -= OnDeath;
        boundPokemon.OnDamageDealt -= OnDamageDealt;
        boundPokemon = null;
    }

    private void OnDeath(DamageInfo info)
    {
        basicRecovery?.Dispose();
        basicRecovery = null;
        StopDashPresentation(false, Vector3.zero);
        foreach (ulong id in new List<ulong>(targets.Keys)) ReleaseTarget(id);
        charge = 0;
        healingAttacks = 0;
        SetFlameChargeSlowArmed(false);
        Publish();
    }

    private void ResetCombatCharges()
    {
        if (charge == 0 && healingAttacks == 0 && !flameSlow) return;
        charge = 0;
        healingAttacks = 0;
        SetFlameChargeSlowArmed(false);
        Publish();
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        UnbindEvents();
        if (IsServer)
        {
            foreach (ulong id in new List<ulong>(targets.Keys)) ReleaseTarget(id);
        }
        else
        {
            foreach (var state in targets.Values)
            {
                state.VisualScope?.Dispose();
                CinderEffectIds.Release(state.MarkId);
                CinderEffectIds.Release(state.BurnId);
            }
            targets.Clear();
        }
        lifetime.Dispose();
        movementLocked = false;
        PresentMovement();
        prefabs.Clear();
        loading.Clear();
    }
}
