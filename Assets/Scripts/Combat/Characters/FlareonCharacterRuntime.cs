using System;
using System.Collections;
using System.Collections.Generic;
using Unite.Combat;
using UnityEngine;
using UnityEngine.AddressableAssets;

public sealed class FlareonCharacterRuntime : CombatCharacterRuntime
{
    [Serializable]
    public sealed class PresentationState
    {
        public long Revision;
        public int AttackCharge;
        public double AttackExpires;
        public int Stacks;
        public double StacksExpire;
        public bool MovementLocked;
        public ulong RecastTarget;
        public double RecastUntil;
        public double RecoveryUntil;
        public double BasicStarted;
        public bool BasicBoosted;
        public Vector3 BasicDirection;
        public double ContactUntil;
        public bool ContactBoosted;
    }

    private readonly FlareonComboState combo = new FlareonComboState();
    private sealed class EffectOwner
    {
        public AbilityInstance Ability;
        public bool Upgraded;
    }

    private readonly Dictionary<FlareonMoveObject, EffectOwner> effects = new Dictionary<FlareonMoveObject, EffectOwner>();
    private readonly Dictionary<FlareonMoveObject, Coroutine> lastingEffects = new Dictionary<FlareonMoveObject, Coroutine>();
    private readonly List<IDisposable> presentationLocks = new List<IDisposable>();
    private Pokemon boundPokemon;
    private FlareonMoveObject stateView;
    private CombatScope stateLoadScope;
    private Coroutine stateLoadRoutine;
    private bool stateLoading;
    private double nextStateLoad;
    private CombatRoutine basicRoutine;
    private long revision;
    private int generation;
    private int movementLocks;
    private bool appliedMovement;
    private Pokemon recastTarget;
    private AbilityInstance recastAbility;
    private bool recastUpgraded;
    private double recastUntil;
    private long recoveryCast;
    private double recoveryUntil;
    private double basicStarted = -100;
    private bool basicBoosted;
    private Vector3 basicDirection = Vector3.forward;
    private double contactUntil;
    private bool contactBoosted;
    private double shownRecastUntil = -1;
    private long shownTraversalCast;
    private int shownTraversalSequence;
    private bool disposed;
    private PresentationState presentation = new PresentationState { BasicStarted = -100, BasicDirection = Vector3.forward, RecastTarget = ulong.MaxValue };

    public FlareonCharacterRuntime(PlayerManager actor) : base(actor) { }

    private double Now => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    private bool IsFlareon => Pokemon != null && Pokemon.BaseStats != null && Pokemon.BaseStats.Passive == AvailablePassives.FlareonPassive;
    public int AttackCharge => IsServer ? combo.AttackCharge : presentation.AttackCharge;
    public int Stacks => IsServer ? combo.Stacks : presentation.Stacks;
    public double StacksExpire => IsServer ? combo.StacksExpire : presentation.StacksExpire;
    public double PresentationNow => Now;
    internal double ServerNow => Now;
    public double BasicStarted => IsServer ? basicStarted : presentation.BasicStarted;
    public bool BasicBoosted => IsServer ? basicBoosted : presentation.BasicBoosted;
    public Vector3 BasicDirection => IsServer ? basicDirection : presentation.BasicDirection;
    public double ContactUntil => IsServer ? contactUntil : presentation.ContactUntil;
    public bool ContactBoosted => IsServer ? contactBoosted : presentation.ContactBoosted;
    public override float MoveInputBufferDuration => FlareonMoveRules.InputBuffer;
    public override bool CanBufferMoveInput
    {
        get
        {
            double until = IsServer ? recoveryUntil : presentation.RecoveryUntil;
            return until > Now && until - Now <= FlareonMoveRules.InputBuffer;
        }
    }
    public Pokemon RecastTarget
    {
        get
        {
            if (IsServer) return recastTarget;
            return NetworkManager != null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(presentation.RecastTarget, out var targetObject)
                ? targetObject.GetComponent<Pokemon>() : null;
        }
    }
    public double RecastUntil => IsServer ? recastUntil : presentation.RecastUntil;
    public bool RecastAvailable => IsServer ? TakedownRecastAvailable
        : Now < presentation.RecastUntil && FlareonMoveRules.CanHit(Actor, RecastTarget);
    internal bool TakedownRecastAvailable => IsServer && Now < recastUntil && FlareonMoveRules.CanHit(Actor, recastTarget)
        && recastAbility != null && ReferenceEquals(Controller.Combat.Loadout.Get(FlareonMoveRules.Slot(FlareonCast.Takedown)), recastAbility)
        && recastAbility.Upgraded == recastUpgraded && !Pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts);

    public override bool PrepareCast(CombatExecutionContext context)
    {
        if (disposed || !IsFlareon || Pokemon.CurrentHp <= 0 || Pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts)) return false;
        FlareonCast move = KindForMove(context.MoveId);
        if (move == FlareonCast.State) return false;
        if (FlareonMoveRules.Driven(move) && Pokemon.HasStatusEffect(StatusType.Immobilized)) return false;
        return !FlareonMoveRules.Targeted(move) || (FlareonMoveRules.CanHit(Actor, context.Target)
            && !context.Target.HasStatusEffect(StatusType.Invisible)
            && Vector3.Distance(transform.position, context.Target.transform.position) <= FlareonMoveRules.Range(move) + 0.4f);
    }

    public override bool RevalidateCast(CombatExecutionContext context) => PrepareCast(context);
    public override CombatExecution CreateExecution(CombatExecutionContext context) => new FlareonExecution(context, this);

    public static FlareonCast KindForMove(AvailableMoves move)
    {
        switch (move)
        {
            case AvailableMoves.FlareonSwift: return FlareonCast.Swift;
            case AvailableMoves.FlareonTackle: return FlareonCast.Tackle;
            case AvailableMoves.FlareonHeatWave: return FlareonCast.HeatWave;
            case AvailableMoves.FlareonTakedown: return FlareonCast.Takedown;
            case AvailableMoves.FlareonFlameThrower: return FlareonCast.Flamethrower;
            case AvailableMoves.FlareonFlareBlitz: return FlareonCast.FlareBlitz;
            case AvailableMoves.FlareonUnite: return FlareonCast.Unite;
            default: return FlareonCast.State;
        }
    }

    public override void Tick()
    {
        if (disposed || !IsSpawned || !IsFlareon || !Pokemon.IsSpawned) return;
        PresentMovement();
        PresentActionLocks();
        PresentHud();
        if (!IsServer) return;
        BindEvents();
        foreach (KeyValuePair<FlareonMoveObject, EffectOwner> entry in new List<KeyValuePair<FlareonMoveObject, EffectOwner>>(effects))
        {
            AbilityInstance current = entry.Value.Ability == null ? null : Controller.Combat.Loadout.Get(entry.Value.Ability.Slot);
            if (entry.Key == null || !entry.Key.IsSpawned || !ReferenceEquals(current, entry.Value.Ability)
                || current == null || current.Upgraded != entry.Value.Upgraded)
                RemoveTrackedEffect(entry.Key);
        }
        if (recastUntil > 0 && !TakedownRecastAvailable) ClearTakedownRecast();
        if (Pokemon.CurrentHp <= 0) return;
        if (stateView == null && !stateLoading && Now >= nextStateLoad) BeginStateLoad();
        int oldCharge = combo.AttackCharge;
        int oldStacks = combo.Stacks;
        combo.Tick(Now);
        if (oldStacks != combo.Stacks) ApplyPassiveStacks();
        if (oldCharge != combo.AttackCharge || oldStacks != combo.Stacks) Publish();
        if (Pokemon.CurrentLevel >= 3 && Pokemon.HasStatChangeWithID(FlareonMoveRules.EeveeBuff))
            Pokemon.RemoveAllStatChangeWithIDRPC(FlareonMoveRules.EeveeBuff);
    }

    internal void BeginRecovery(long castId, float duration)
    {
        if (!IsServer) return;
        recoveryCast = castId;
        recoveryUntil = Now + duration;
        Publish();
    }

    internal void EndRecovery(long castId)
    {
        if (!IsServer || recoveryCast != castId) return;
        recoveryCast = 0;
        recoveryUntil = 0;
        Publish();
    }

    private void PresentHud()
    {
        if (!IsOwner) return;
        (Controller.BasicAttack as FlareonBasicAtk)?.ApplySnapshot(AttackCharge);
        double until = RecastUntil;
        if (shownRecastUntil == until || !Actor.IsLocalHuman || BattleUIManager.instance == null) return;
        shownRecastUntil = until;
        BattleUIManager.instance.ShowMoveSecondaryCooldown(0, Mathf.Max(0, (float)(until - Now)));
    }

    private void BeginStateLoad()
    {
        stateLoading = true;
        nextStateLoad = Now + 2;
        stateLoadScope = new CombatScope();
        stateLoadRoutine = Actor.StartCoroutine(LoadStateView(stateLoadScope, generation));
    }

    private IEnumerator LoadStateView(CombatScope scope, int loadGeneration)
    {
        string path = FlareonMoveRules.Path(FlareonCast.State);
        GameObject prefab = null;
        yield return Controller.Combat.Spawner.Load(path, scope, loaded => prefab = loaded);
        if (!scope.IsDisposed && !disposed && IsServer && IsSpawned && Pokemon.CurrentHp > 0 && loadGeneration == generation
            && prefab != null && prefab.GetComponent<FlareonMoveObject>() != null && prefab.GetComponent<Unity.Netcode.NetworkObject>() != null)
        {
            GameObject spawned = Controller.Combat.Spawner.Spawn(prefab, transform.position, transform.rotation, scope, false);
            if (spawned != null)
            {
                stateView = spawned.GetComponent<FlareonMoveObject>();
                stateView.HoldAsset(Addressables.LoadAssetAsync<GameObject>(path));
                stateView.InitializeCombat(Actor, FlareonCast.State, transform.forward, null, false);
            }
        }
        scope.Dispose();
        if (ReferenceEquals(stateLoadScope, scope))
        {
            stateLoadScope = null;
            stateLoadRoutine = null;
            stateLoading = false;
        }
    }

    private void ClearStateView()
    {
        if (stateLoadRoutine != null) Actor.StopCoroutine(stateLoadRoutine);
        stateLoadRoutine = null;
        stateLoading = false;
        stateLoadScope?.Dispose();
        stateLoadScope = null;
        if (stateView != null && stateView.IsSpawned && IsServer) stateView.Despawn();
        stateView = null;
    }

    internal IDisposable AcquireMovementLock()
    {
        movementLocks++;
        Publish();
        PresentMovement();
        return new CombatCallbackLease(() =>
        {
            movementLocks = Math.Max(0, movementLocks - 1);
            Publish();
            PresentMovement();
        });
    }

    private void PresentMovement()
    {
        if ((!IsServer && !IsOwner) || Actor.PlayerMovement == null) return;
        bool locked = IsServer ? movementLocks > 0 : presentation.MovementLocked;
        if (locked == appliedMovement) return;
        if (locked) Actor.PlayerMovement.AddMovementRestriction();
        else Actor.PlayerMovement.RemoveMovementRestriction();
        appliedMovement = locked;
    }

    private void PresentActionLocks()
    {
        if (!IsOwner || IsServer) return;
        if (presentation.MovementLocked && presentationLocks.Count == 0)
        {
            for (int slot = 0; slot < 3; slot++) presentationLocks.Add(Controller.AcquireMoveStatus(slot, ActionStatusType.Busy));
            presentationLocks.Add(Controller.BasicAttackStatus.AcquireStatus(ActionStatusType.Busy));
            presentationLocks.Add(Controller.BattleItemStatus.AcquireStatus(ActionStatusType.Busy));
            presentationLocks.Add(Actor.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
            presentationLocks.Add(Actor.RecallStatus.AcquireStatus(ActionStatusType.Busy));
        }
        else if (!presentation.MovementLocked && presentationLocks.Count > 0)
            ClearPresentationLocks();
    }

    private void ClearPresentationLocks()
    {
        foreach (IDisposable lease in presentationLocks) lease.Dispose();
        presentationLocks.Clear();
    }

    public void ReceiveTraversalPosition(long castId, int sequence, Vector3 position, Vector3 direction)
    {
        if (disposed || IsServer || !IsOwner || !IsSpawned || castId < shownTraversalCast
            || (castId == shownTraversalCast && sequence <= shownTraversalSequence)
            || !FlareonMoveRules.Finite(position) || !FlareonMoveRules.Finite(direction)) return;
        shownTraversalCast = castId;
        shownTraversalSequence = sequence;
        Actor.transform.SetPositionAndRotation(position, Quaternion.LookRotation(FlareonMoveRules.Direction(direction, Actor.transform.forward)));
    }

    internal void ArmTakedownRecast(Pokemon target, float duration)
    {
        if (!IsServer || !FlareonMoveRules.CanHit(Actor, target)) return;
        recastAbility = Controller.Combat.Loadout.Get(FlareonMoveRules.Slot(FlareonCast.Takedown));
        if (recastAbility == null) return;
        recastUpgraded = recastAbility.Upgraded;
        recastTarget = target;
        recastUntil = Now + duration;
        Publish();
    }

    internal void ClearTakedownRecast()
    {
        if (recastTarget == null && recastUntil == 0) return;
        recastTarget = null;
        recastAbility = null;
        recastUpgraded = false;
        recastUntil = 0;
        Publish();
    }

    internal void TrackEffect(FlareonMoveObject effect, int slot)
    {
        if (effect == null) return;
        AbilityInstance ability = Controller.Combat.Loadout.Get(slot);
        effects[effect] = new EffectOwner { Ability = ability, Upgraded = ability != null && ability.Upgraded };
    }

    internal void StartSwiftField(FlareonMoveObject effect)
    {
        if (effect != null) lastingEffects[effect] = Actor.StartCoroutine(RunSwiftField(effect));
    }

    internal void EndEffectAfter(FlareonMoveObject effect, float delay)
    {
        if (effect != null) lastingEffects[effect] = Actor.StartCoroutine(WaitForEffectEnd(effect, delay));
    }

    private IEnumerator WaitForEffectEnd(FlareonMoveObject effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        EndLastingEffect(effect);
    }

    private IEnumerator RunSwiftField(FlareonMoveObject effect)
    {
        for (int pulse = 0; pulse < 10; pulse++)
        {
            yield return new WaitForSeconds(0.4f);
            if (disposed || !IsSpawned || Pokemon.CurrentHp <= 0 || effect == null || !effect.IsSpawned) break;
            effect.MoveCombatObject(transform.position, transform.forward);
            effect.SetCombatPhase((byte)(pulse + 1));
            effect.DamageCombatArea(this, FlareonCast.Swift);
        }
        EndLastingEffect(effect);
    }

    internal void StartHeatWaveField(FlareonMoveObject effect, bool upgraded)
    {
        if (effect == null) return;
        Pokemon.RemoveAllStatChangeWithIDRPC(FlareonMoveRules.SpeedBuff);
        Pokemon.AddStatChange(new StatChange(FlareonMoveRules.HeatSpeedBonus, Stat.Speed, 6, true, true, true, FlareonMoveRules.SpeedBuff));
        lastingEffects[effect] = Actor.StartCoroutine(RunHeatWaveField(effect, upgraded));
    }

    private IEnumerator RunHeatWaveField(FlareonMoveObject effect, bool upgraded)
    {
        for (int pulse = 0; pulse < FlareonMoveRules.HeatPulses(upgraded); pulse++)
        {
            yield return new WaitForSeconds(pulse == 0 ? 0.35f : FlareonMoveRules.HeatInterval(upgraded));
            if (disposed || !IsSpawned || Pokemon.CurrentHp <= 0 || effect == null || !effect.IsSpawned) break;
            effect.MoveCombatObject(transform.position, transform.forward);
            effect.SetCombatPhase((byte)(pulse + 1));
            effect.DamageCombatArea(this, FlareonCast.HeatWave);
        }
        if (!disposed && IsSpawned && effect != null && effect.IsSpawned) yield return new WaitForSeconds(0.25f);
        EndLastingEffect(effect);
    }

    private void EndLastingEffect(FlareonMoveObject effect)
    {
        if (!ReferenceEquals(effect, null)) lastingEffects.Remove(effect);
        if (!ReferenceEquals(effect, null)) effects.Remove(effect);
        if (effect != null && effect.IsSpawned) effect.Despawn();
    }

    private void RemoveTrackedEffect(FlareonMoveObject effect)
    {
        if (ReferenceEquals(effect, null)) return;
        if (lastingEffects.TryGetValue(effect, out Coroutine routine))
        {
            if (routine != null) Actor.StopCoroutine(routine);
            lastingEffects.Remove(effect);
        }
        effects.Remove(effect);
        if (effect != null && effect.IsSpawned) effect.Despawn();
    }

    private void ClearEffects()
    {
        foreach (Coroutine routine in lastingEffects.Values) if (routine != null) Actor.StopCoroutine(routine);
        lastingEffects.Clear();
        foreach (FlareonMoveObject effect in effects.Keys) if (effect != null && effect.IsSpawned) effect.Despawn();
        effects.Clear();
    }

    public void PerformBasicAttack(Pokemon target)
    {
        if (disposed || !IsServer || !IsSpawned || !IsFlareon || Pokemon.CurrentHp <= 0
            || Pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts)
            || !FlareonMoveRules.CanHit(Actor, target)
            || !CombatTargeting.WithinMeleeRange(transform.position, target.transform.position, FlareonMoveRules.BasicRange)) return;

        int oldStacks = combo.Stacks;
        combo.Tick(Now);
        if (oldStacks != combo.Stacks) ApplyPassiveStacks();
        bool boosted = combo.AttackCharge == 2;
        Vector3 direction = FlareonMoveRules.Direction(target.transform.position - transform.position, transform.forward);
        basicStarted = Now;
        basicBoosted = boosted;
        basicDirection = direction;
        Publish();
        string animation = Pokemon.CurrentLevel < 3 ? boosted ? "ani_atk6_bat_0133" : "ani_atk4_bat_0133" : string.Empty;
        basicRoutine?.Dispose();
        Controller.PresentServerAttackRPC(true, direction, animation, 0.14f);
        basicRoutine = new CombatRoutine(Actor, ResolveBasicAttack(direction, boosted, generation, animation));
    }

    private IEnumerator ResolveBasicAttack(Vector3 direction, bool boosted, int attackGeneration, string animation)
    {
        try
        {
            yield return new WaitForSeconds(0.1f);
            if (disposed || !IsServer || !IsSpawned || !IsFlareon || Pokemon.CurrentHp <= 0
                || attackGeneration != generation || Pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts)) yield break;
            bool connected = false;
            Vector3 origin = transform.position;
            foreach (Pokemon target in CombatTargeting.SphereTargets(origin + direction * 1.35f + Vector3.up * 0.5f, 1.15f))
            {
                if (target == null || !CombatTargeting.WithinMeleeRange(origin, target.transform.position, FlareonMoveRules.BasicRange)) continue;
                if (!HitTarget(target, FlareonMoveRules.Damage(Pokemon.NetworkObjectId, FlareonCast.Basic, boosted: boosted))) continue;
                connected = true;
                if (boosted && Pokemon.CurrentLevel >= 3) target.RefreshDamageOverTime(StatusType.Burned, Pokemon.NetworkObjectId, 2);
            }
            if (connected)
            {
                contactUntil = Now + 0.12;
                contactBoosted = boosted;
                combo.ConfirmBasic(Now);
                if (boosted)
                {
                    Controller.Combat.ReduceCooldown(0, 0.5);
                    Controller.Combat.ReduceCooldown(1, 0.5);
                }
                Publish();
            }
            yield return new WaitForSeconds(0.04f);
        }
        finally
        {
            if (IsSpawned && Pokemon.CurrentHp > 0 && !Pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts))
            {
                Controller.PresentServerAttackRPC(false, Vector3.zero, string.Empty);
                if (!string.IsNullOrEmpty(animation))
                    Actor.AnimationManager?.RecoverAnimationRPC(animation, Pokemon.CurrentLevel < 3 ? "ani_idle_bat_0133" : "pm0136_ba10_waitA01", 0.06f);
            }
            else if (IsServer && IsSpawned)
                Controller.PresentServerAttackRPC(false, Vector3.zero, string.Empty);
        }
    }

    public void ResetBasicAttack()
    {
        if (!IsServer) return;
        basicRoutine?.Dispose();
        basicRoutine = null;
        combo.ResetBasic();
        Publish();
    }

    public bool HitTarget(Pokemon target, DamageInfo damage, float burnDuration = 0)
    {
        if (!IsServer || !FlareonMoveRules.CanHit(Actor, target)) return false;
        bool applied = false;
        void Record(Pokemon victim, DamageInfo actual, int amount)
        {
            if (victim != target || actual.attackerId != damage.attackerId || actual.ratio != damage.ratio || actual.baseDmg != damage.baseDmg) return;
            applied = true;
            if (burnDuration > 0 && victim.CurrentHp > 0) FlareonMoveRules.Burn(victim, Pokemon.NetworkObjectId, burnDuration);
        }
        Pokemon.OnHealthDamageDealt += Record;
        try { target.TakeDamageRPC(damage); }
        finally { Pokemon.OnHealthDamageDealt -= Record; }
        return applied;
    }

    private void BindEvents()
    {
        if (boundPokemon == Pokemon) return;
        UnbindEvents();
        boundPokemon = Pokemon;
        if (boundPokemon == null) return;
        boundPokemon.OnDeath += OnDeath;
        boundPokemon.OnHpChange += OnHpChange;
        boundPokemon.OnDamageDealt += OnDamageDealt;
    }

    private void UnbindEvents()
    {
        if (boundPokemon == null) return;
        boundPokemon.OnDeath -= OnDeath;
        boundPokemon.OnHpChange -= OnHpChange;
        boundPokemon.OnDamageDealt -= OnDamageDealt;
        boundPokemon = null;
    }

    private void OnHpChange()
    {
        if (!IsServer || Pokemon.CurrentHp <= 0 || Pokemon.CurrentLevel >= 3 || Pokemon.CurrentHp >= Pokemon.GetMaxHp() * 0.5f
            || Pokemon.HasStatChangeWithID(FlareonMoveRules.EeveeBuff)) return;
        Pokemon.AddStatChange(new StatChange(10, Stat.Attack, 5, true, true, true, FlareonMoveRules.EeveeBuff));
    }

    private void OnDamageDealt(ulong targetId, DamageInfo damage)
    {
        if (!IsServer || Pokemon.CurrentHp <= 0 || Pokemon.CurrentLevel < 3
            || (damage.proprieties & (DamageProprieties.Unused_2 | DamageProprieties.IsMuscleBand | DamageProprieties.IsHeal)) != 0
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var targetObject)
            || !targetObject.TryGetComponent(out Pokemon target) || target.Type != PokemonType.Player
            || !target.HasStatusEffect(StatusType.Burned)) return;
        int oldStacks = combo.Stacks;
        combo.AddStack(Now);
        if (oldStacks != combo.Stacks) ApplyPassiveStacks();
        Publish();
    }

    private void ApplyPassiveStacks()
    {
        if (!IsServer || !IsSpawned || Pokemon == null || !Pokemon.IsSpawned) return;
        Pokemon.RemoveAllStatChangeWithIDRPC(FlareonMoveRules.AttackBuff);
        if (combo.Stacks > 0)
            Pokemon.AddStatChange(new StatChange((short)(combo.Stacks * 5), Stat.Attack, 0, false, true, true, FlareonMoveRules.AttackBuff));
    }

    private void OnDeath(DamageInfo damage)
    {
        if (!IsServer) return;
        generation++;
        recoveryCast = 0;
        recoveryUntil = 0;
        basicStarted = -100;
        contactUntil = 0;
        ClearTakedownRecast();
        ClearEffects();
        ClearStateView();
        basicRoutine?.Dispose();
        basicRoutine = null;
        combo.Clear();
        ApplyPassiveStacks();
        Publish();
    }

    private void Publish() { if (!disposed && IsServer) revision++; }

    public override string CapturePresentationState() => JsonUtility.ToJson(new PresentationState
    {
        Revision = revision,
        AttackCharge = combo.AttackCharge,
        AttackExpires = combo.AttackExpires,
        Stacks = combo.Stacks,
        StacksExpire = combo.StacksExpire,
        MovementLocked = movementLocks > 0,
        RecastTarget = recastTarget != null ? recastTarget.NetworkObjectId : ulong.MaxValue,
        RecastUntil = recastUntil,
        RecoveryUntil = recoveryUntil,
        BasicStarted = basicStarted,
        BasicBoosted = basicBoosted,
        BasicDirection = basicDirection,
        ContactUntil = contactUntil,
        ContactBoosted = contactBoosted
    });

    public override void ApplyPresentationState(string state)
    {
        if (disposed || IsServer || string.IsNullOrEmpty(state)) return;
        PresentationState snapshot;
        try { snapshot = JsonUtility.FromJson<PresentationState>(state); }
        catch (ArgumentException) { return; }
        if (snapshot == null || snapshot.Revision < presentation.Revision
            || double.IsNaN(snapshot.AttackExpires) || double.IsInfinity(snapshot.AttackExpires)
            || double.IsNaN(snapshot.StacksExpire) || double.IsInfinity(snapshot.StacksExpire)
            || double.IsNaN(snapshot.RecastUntil) || double.IsInfinity(snapshot.RecastUntil)
            || double.IsNaN(snapshot.RecoveryUntil) || double.IsInfinity(snapshot.RecoveryUntil)
            || double.IsNaN(snapshot.BasicStarted) || double.IsInfinity(snapshot.BasicStarted)
            || double.IsNaN(snapshot.ContactUntil) || double.IsInfinity(snapshot.ContactUntil)
            || !FlareonMoveRules.Finite(snapshot.BasicDirection)
            || snapshot.AttackCharge < 0 || snapshot.AttackCharge > 2 || snapshot.Stacks < 0 || snapshot.Stacks > 3) return;
        presentation = snapshot;
        PresentMovement();
        PresentActionLocks();
        PresentHud();
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        generation++;
        ClearTakedownRecast();
        ClearEffects();
        ClearStateView();
        movementLocks = 0;
        presentation.MovementLocked = false;
        PresentMovement();
        ClearPresentationLocks();
        basicRoutine?.Dispose();
        basicRoutine = null;
        combo.Clear();
        ApplyPassiveStacks();
        UnbindEvents();
        if (IsOwner && Actor != null && Actor.IsLocalHuman && BattleUIManager.instance != null)
            BattleUIManager.instance.ShowMoveSecondaryCooldown(0, 0);
    }
}
