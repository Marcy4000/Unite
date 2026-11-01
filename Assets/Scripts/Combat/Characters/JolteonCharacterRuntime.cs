using System;
using System.Collections;
using System.Collections.Generic;
using Unite.Combat;
using Unity.Netcode;
using UnityEngine;

public sealed class JolteonCharacterRuntime : CombatCharacterRuntime
{
    [Serializable]
    public sealed class PresentationState
    {
        public long Revision;
        public float PassiveCharge;
        public double OverchargeUntil;
        public byte AttackCharge;
        public bool Evolved;
        public bool MovementLocked;
        public bool DashActive;
        public bool RecastAvailable;
        public int RecastMove;
        public double RecastUntil;
        public bool RecastEmpowered;
        public long UniteCastId;
        public long TraversalCastId;
        public int TraversalSequence;
        public bool TraversalActive;
    }

    private const string SwiftPath = "Assets/Prefabs/Objects/Moves/Jolteon/JoltSwift.prefab";
    private readonly CombatScope lifetime = new CombatScope();
    private readonly JoltChargeState passiveCharge = new JoltChargeState();
    private readonly Dictionary<ulong, float> nextDamageCharge = new Dictionary<ulong, float>();
    private readonly Dictionary<JoltEffect, JoltEffectRequest> persistentEffects = new Dictionary<JoltEffect, JoltEffectRequest>();
    private readonly HashSet<JoltEffectRequest> effectRequests = new HashSet<JoltEffectRequest>();
    private readonly HashSet<JoltSwiftProjectile> swiftProjectiles = new HashSet<JoltSwiftProjectile>();
    private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    private readonly StatChange eeveeSpeed = new StatChange(10, Stat.Speed, 0, false, true, true, 9);
    private Pokemon boundPokemon;
    private GameObject swiftPrefab;
    private CombatRoutine basicRecovery;
    private JolteonExecution uniteExecution;
    private long revision;
    private long uniteCastId;
    private long traversalCastId, receivedTraversalCastId;
    private int traversalSequence, receivedTraversalSequence;
    private bool traversalActive, receivedTraversalActive;
    private byte attackCharge;
    private float chargeBudget, budgetUntil, nextReadyVisual, nextSnapshot;
    private float nextSteeringSend, nextSteeringReceive;
    private int movementLocks;
    private bool evolved, evolutionInitialized, movementLocked, appliedMovement, dashActive, disposed;
    private bool recastAvailable, recastEmpowered;
    private AvailableMoves recastMove;
    private double recastUntil, observedOverchargeUntil, shownRecastUntil;
    private float observedPassiveCharge;

    public JolteonCharacterRuntime(PlayerManager actor) : base(actor)
    {
        BindEvents();
    }

    private double Now => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    private bool IsJolteon => Pokemon != null && Pokemon.BaseStats != null && Pokemon.BaseStats.Passive == AvailablePassives.JolteonPassive;
    public bool Evolved => evolved;
    public byte AttackCharge => attackCharge;
    public float PassiveAmount => IsServer ? passiveCharge.Amount : observedPassiveCharge;
    public bool PassiveReady => PassiveAmount >= 100f;
    public bool DashActive => dashActive;
    public bool RecastAvailable => recastAvailable && Now < recastUntil;
    public AvailableMoves RecastMove => recastMove;
    public double RecastUntil => recastUntil;
    public bool RecastEmpowered => recastEmpowered;
    public double OverchargeUntil => IsServer ? Now + Math.Max(0f, passiveCharge.OverchargeUntil - Time.time) : observedOverchargeUntil;

    public override bool PrepareCast(CombatExecutionContext context)
    {
        return !disposed && IsServer && IsJolteon && Pokemon.CurrentHp > 0;
    }

    public override bool RevalidateCast(CombatExecutionContext context) => PrepareCast(context);
    public override CombatExecution CreateExecution(CombatExecutionContext context) => new JolteonExecution(context, this);

    public override void Tick()
    {
        if (disposed || !IsSpawned || Pokemon == null || !IsJolteon) return;
        if (IsServer)
        {
            BindEvents();
            InitializeEvolution();
            swiftProjectiles.RemoveWhere(projectile => projectile == null || !projectile.IsSpawned);
            if (Pokemon.CurrentHp > 0 && evolved)
            {
                bool moving = dashActive || (Actor.PlayerMovement != null && Actor.PlayerMovement.IsMoving);
                passiveCharge.Add((moving ? 6f : 2f) * Time.deltaTime);
                UpdateReadyEffect();
                if (Time.time >= nextSnapshot)
                {
                    nextSnapshot = Time.time + 0.1f;
                    Publish();
                }
            }
        }
        if (IsOwner && uniteCastId != 0 && Time.time >= nextSteeringSend)
        {
            nextSteeringSend = Time.time + 0.05f;
            Controller.JolteonSteeringRPC(uniteCastId, Actor.MovementInput);
        }
        Present();
    }

    private void BindEvents()
    {
        if (boundPokemon == Pokemon) return;
        UnbindEvents();
        boundPokemon = Pokemon;
        if (boundPokemon == null) return;
        boundPokemon.OnEvolution += OnEvolution;
        boundPokemon.OnDamageDealt += OnDamageDealt;
        boundPokemon.OnOtherPokemonKilled += OnKnockout;
        boundPokemon.OnDeath += OnDeath;
        evolved = boundPokemon.CurrentLevel >= 3;
        evolutionInitialized = false;
    }

    private void UnbindEvents()
    {
        if (boundPokemon == null) return;
        boundPokemon.OnEvolution -= OnEvolution;
        boundPokemon.OnDamageDealt -= OnDamageDealt;
        boundPokemon.OnOtherPokemonKilled -= OnKnockout;
        boundPokemon.OnDeath -= OnDeath;
        boundPokemon = null;
        evolutionInitialized = false;
    }

    private void InitializeEvolution()
    {
        if (evolutionInitialized || boundPokemon == null) return;
        evolutionInitialized = true;
        evolved = boundPokemon.CurrentLevel >= 3;
        if (!evolved) boundPokemon.AddStatChange(eeveeSpeed);
    }

    private void OnEvolution()
    {
        if (Pokemon == null || Pokemon.CurrentEvolution == null || Pokemon.CurrentEvolution.level < 3) return;
        evolved = true;
        if (IsServer) Pokemon.RemoveStatChangeWithIDRPC(eeveeSpeed.ID);
        Publish();
    }

    public void PerformBasicAttack(Pokemon target)
    {
        if (!IsServer || disposed || !IsSpawned || !IsJolteon || Pokemon.CurrentHp <= 0) return;
        if (target != null && (!JoltMoveRuntime.CanHit(Actor, target) || target.HasStatusEffect(StatusType.Invisible)
            || !CombatTargeting.WithinMeleeRange(transform.position, target.transform.position, 2.5f))) return;
        if (target == null && Actor.PlayerMovement.IsMoving) return;
        Vector3 facing = target != null ? target.transform.position - transform.position : transform.forward;
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing.normalized);
        byte previous = attackCharge;
        DamageInfo damage = previous == 2
            ? new DamageInfo(Pokemon.NetworkObjectId, 0.6f, 7, 160, DamageType.Physical, DamageProprieties.IsBasicAttack)
            : new DamageInfo(Pokemon.NetworkObjectId, 0.4f, 5, 120, DamageType.Physical, DamageProprieties.IsBasicAttack);
        var hitTargets = new HashSet<ulong>();
        Vector3 center = transform.position + transform.forward * 1.377f;
        foreach (Collider collider in Physics.OverlapSphere(center, 1f))
        {
            if (!collider.TryGetComponent(out Pokemon hit) || !JoltMoveRuntime.CanHit(Actor, hit)
                || hit.HasStatusEffect(StatusType.Invisible) || !hitTargets.Add(hit.NetworkObjectId)
                || !CombatTargeting.WithinMeleeRange(transform.position, hit.transform.position, 2.5f)) continue;
            hit.TakeDamageRPC(damage);
        }
        attackCharge = (byte)((previous + 1) % 3);
        string animation = evolved
            ? (previous == 0 ? "pm0135_00_ba20_buturi01" : previous == 1 ? "pm0135_00_ba21_tokusyu01" : "pm0135_00_ba21_tokusyu02")
            : $"ani_atk{previous + 4}_bat_0133";
        basicRecovery?.Dispose();
        basicRecovery = new CombatRoutine(Actor, BasicRecovery(facing, animation));
        Publish();
    }

    private IEnumerator BasicRecovery(Vector3 facing, string animation)
    {
        float duration = 0.5f * Controller.GetAtkSpeedCooldown();
        bool transition = !evolved;
        bool completed = false;
        Controller.PresentServerAttackRPC(true, facing, animation, duration);
        try { yield return new WaitForSeconds(duration); completed = true; }
        finally
        {
            if (Actor != null && IsSpawned)
            {
                Controller.PresentServerAttackRPC(false, Vector3.zero, "");
                if (completed && Pokemon != null && Pokemon.CurrentHp > 0
                    && !Pokemon.HasAnyStatusEffect(JoltMoveRuntime.MovementInterrupts))
                {
                    if (transition) Actor.AnimationManager?.SetTrigger("Transition");
                    else Actor.AnimationManager?.RecoverAnimationRPC(animation, "IdleTree", 0.06f);
                }
            }
        }
    }

    public void ResetBasicCharge()
    {
        if (!IsServer || disposed) return;
        basicRecovery?.Dispose();
        basicRecovery = null;
        attackCharge = 0;
        Publish();
    }

    private bool TryTarget(ulong targetId, out Pokemon target)
    {
        target = null;
        return NetworkManager != null && NetworkManager.SpawnManager != null
            && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject targetObject)
            && targetObject.TryGetComponent(out target);
    }

    private void OnDamageDealt(ulong targetId, DamageInfo damage)
    {
        if (!IsServer || disposed || Pokemon.CurrentHp <= 0 || !TryTarget(targetId, out Pokemon target)
            || damage.proprieties.HasFlag(DamageProprieties.IsHeal)) return;
        float now = Time.time;
        if (nextDamageCharge.TryGetValue(targetId, out float next) && now < next) return;
        nextDamageCharge[targetId] = now + (damage.proprieties.HasFlag(JoltMoveRules.PeriodicDamage) ? 1f : 0.25f);
        if (now >= budgetUntil) { chargeBudget = 0f; budgetUntil = now + 1f; }
        float amount = Mathf.Min(target.Type == PokemonType.Player ? 8f : 4f, 24f - chargeBudget);
        chargeBudget += amount;
        passiveCharge.Add(amount);
        Publish();
    }

    private void OnKnockout(ulong targetId)
    {
        if (!IsServer || disposed || Pokemon.CurrentHp <= 0 || !TryTarget(targetId, out Pokemon target) || target.Type != PokemonType.Player) return;
        passiveCharge.Add(20f);
        Publish();
    }

    public bool ConsumePassiveCharge()
    {
        if (!IsServer || disposed) return false;
        bool result = passiveCharge.Consume(Time.time);
        if (!passiveCharge.Ready) CancelPersistentEffect(JoltEffect.Ready);
        Publish();
        return result;
    }

    public void ReducePassiveCharge(float amount)
    {
        if (!IsServer || disposed) return;
        passiveCharge.Reduce(amount, Time.time);
        if (!passiveCharge.Ready) CancelPersistentEffect(JoltEffect.Ready);
        Publish();
    }

    public void BeginUniteOvercharge(float duration)
    {
        if (!IsServer || disposed) return;
        passiveCharge.Overcharge(Time.time, duration);
        ReplacePersistentEffect(JoltEffect.Overcharge, transform.forward, true, false);
        Publish();
    }

    private void UpdateReadyEffect()
    {
        if (!passiveCharge.Ready) { CancelPersistentEffect(JoltEffect.Ready); return; }
        if (!persistentEffects.ContainsKey(JoltEffect.Ready) || Time.time >= nextReadyVisual)
        {
            ReplacePersistentEffect(JoltEffect.Ready, transform.forward, false, false);
            nextReadyVisual = Time.time + 60f;
        }
    }

    public void SetDashActive(bool active)
    {
        if (!IsServer || disposed || dashActive == active) return;
        dashActive = active;
        Publish();
    }

    public void SetTraversalPosition(long castId, int sequence, Vector3 position, Vector3 direction, bool active)
    {
        if (!IsServer || disposed || !IsSpawned || castId <= 0 || sequence <= 0
            || castId < traversalCastId || (castId == traversalCastId && sequence <= traversalSequence)
            || !CombatTargeting.Finite(position) || !CombatTargeting.Finite(direction)) return;
        bool changed = traversalCastId != castId || traversalActive != active;
        traversalCastId = castId;
        traversalSequence = sequence;
        traversalActive = active;
        Controller.JolteonServerPositionRPC(castId, sequence, position, direction, active);
        if (changed) Publish();
    }

    public void ReceiveTraversalPosition(long castId, int sequence, Vector3 position, Vector3 direction, bool active)
    {
        if (disposed || IsServer || !IsOwner || !IsSpawned || castId <= 0 || sequence <= 0
            || castId < receivedTraversalCastId || (castId == receivedTraversalCastId && sequence <= receivedTraversalSequence)
            || !CombatTargeting.Finite(position) || !CombatTargeting.Finite(direction)) return;
        receivedTraversalCastId = castId;
        receivedTraversalSequence = sequence;
        receivedTraversalActive = active;
        direction.y = 0f;
        Actor.transform.position = position;
        if (direction.sqrMagnitude > 0.001f) Actor.transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    public void BeginUnite(JolteonExecution execution, long castId)
    {
        if (!IsServer || disposed || execution == null || castId <= 0) return;
        uniteExecution = execution;
        uniteCastId = castId;
        nextSteeringReceive = 0f;
        Publish();
    }

    public void EndUnite(JolteonExecution execution)
    {
        if (!IsServer || uniteExecution != execution) return;
        uniteExecution = null;
        uniteCastId = 0;
        Publish();
    }

    public void ReceiveUniteSteering(long castId, Vector2 input)
    {
        if (!IsServer || disposed || uniteExecution == null || uniteCastId != castId || Time.time < nextSteeringReceive
            || float.IsNaN(input.x) || float.IsNaN(input.y) || float.IsInfinity(input.x) || float.IsInfinity(input.y)) return;
        nextSteeringReceive = Time.time + 0.025f;
        uniteExecution.SetSteering(Vector2.ClampMagnitude(input, 1f));
    }

    public void SetRecastWindow(AvailableMoves move, bool available, double until, bool empowered)
    {
        if (!IsServer || disposed) return;
        recastMove = move;
        recastAvailable = available;
        recastUntil = until;
        recastEmpowered = empowered;
        Publish();
    }

    public IEnumerator PrepareExecutionAssets(CombatExecutionContext context, Action<bool> completed)
    {
        string path = EffectPathFor(context.MoveId);
        if (path == null || disposed || !IsServer) { completed(false); yield break; }
        prefabs.TryGetValue(path, out GameObject loaded);
        if (loaded == null)
        {
            yield return Controller.Combat.Spawner.Load(path, lifetime, asset => loaded = asset);
            if (loaded != null && !disposed) prefabs[path] = loaded;
        }
        bool valid = !disposed && !context.Scope.IsDisposed && loaded != null && loaded.GetComponent<NetworkObject>() != null;
        if (valid && context.MoveId == AvailableMoves.JoltSwift)
        {
            valid = loaded.GetComponent<JoltSwiftProjectile>() != null;
            if (valid) swiftPrefab = loaded;
        }
        else if (valid) valid = loaded.GetComponent<JoltEffectObject>() != null;
        completed(valid);
    }

    private static string EffectPathFor(AvailableMoves move)
    {
        switch (move)
        {
            case AvailableMoves.JoltSwift: return SwiftPath;
            case AvailableMoves.JoltAgility: return JoltMoveRules.EffectPath(JoltEffect.Agility);
            case AvailableMoves.JoltCharge: return JoltMoveRules.EffectPath(JoltEffect.Charge);
            case AvailableMoves.JoltThunderFang: return JoltMoveRules.EffectPath(JoltEffect.ThunderFang);
            case AvailableMoves.JoltDischarge: return JoltMoveRules.EffectPath(JoltEffect.Discharge);
            case AvailableMoves.JoltElectroWeb: return JoltMoveRules.EffectPath(JoltEffect.Electroweb);
            case AvailableMoves.JolteonUnite: return JoltMoveRules.EffectPath(JoltEffect.LightningBlitz);
            default: return null;
        }
    }

    public bool SpawnSwiftProjectile(Vector3 direction)
    {
        if (!IsServer || disposed || swiftPrefab == null || !IsSpawned || Pokemon.CurrentHp <= 0) return false;
        Vector3 origin = transform.position + Vector3.up;
        GameObject instance = Controller.Combat.Spawner.Spawn(swiftPrefab, origin, Quaternion.identity, lifetime, false);
        JoltSwiftProjectile projectile = instance != null ? instance.GetComponent<JoltSwiftProjectile>() : null;
        if (projectile == null) return false;
        swiftProjectiles.Add(projectile);
        projectile.InitializeServer(Actor, direction, true, swiftPrefab);
        return true;
    }

    public void TrackSwiftProjectile(JoltSwiftProjectile projectile)
    {
        if (IsServer && !disposed && projectile != null) swiftProjectiles.Add(projectile);
    }

    public bool SpawnElectroweb(Vector3 direction, bool empowered, bool upgraded)
    {
        return StartEffect(JoltEffect.Electroweb, direction, empowered, upgraded, false, false) != null;
    }

    public IDisposable SpawnCastEffect(JoltEffect effect, Vector3 direction, bool empowered, bool upgraded, bool second)
    {
        JoltEffectRequest request = StartEffect(effect, direction, empowered, upgraded, second, false, true);
        return request == null ? null : new CombatCallbackLease(request.Cancel);
    }

    public void ReplacePersistentEffect(JoltEffect effect, Vector3 direction, bool empowered, bool upgraded)
    {
        CancelPersistentEffect(effect);
        StartEffect(effect, direction, empowered, upgraded, false, true);
    }

    private JoltEffectRequest StartEffect(JoltEffect effect, Vector3 direction, bool empowered, bool upgraded, bool second, bool persistent, bool prepared = false)
    {
        if (!IsServer || disposed || !IsSpawned || Pokemon == null || Pokemon.CurrentHp <= 0) return null;
        JoltEffectRequest request = null;
        request = new JoltEffectRequest(Actor, () =>
        {
            effectRequests.Remove(request);
            if (persistentEffects.TryGetValue(effect, out JoltEffectRequest current) && current == request)
                persistentEffects.Remove(effect);
        });
        effectRequests.Add(request);
        if (persistent) persistentEffects[effect] = request;
        if (prepared)
        {
            if (!request.LoadPrepared(effect, transform.position, direction, empowered, upgraded, second)) return null;
        }
        else request.Load(effect, transform.position, direction, empowered, upgraded, second);
        return request;
    }

    internal void TrackEffectRequest(JoltEffectRequest request)
    {
        if (request == null || !IsServer) return;
        if (disposed) request.Cancel(); else effectRequests.Add(request);
    }

    internal void ForgetEffectRequest(JoltEffectRequest request) => effectRequests.Remove(request);

    private void CancelPersistentEffect(JoltEffect effect)
    {
        if (persistentEffects.TryGetValue(effect, out JoltEffectRequest request)) request.Cancel();
        persistentEffects.Remove(effect);
    }

    public IDisposable AcquireMovementLock()
    {
        if (!IsServer || disposed) return new CombatCallbackLease(null);
        movementLocks++;
        movementLocked = true;
        Publish();
        return new CombatCallbackLease(() =>
        {
            movementLocks = Mathf.Max(0, movementLocks - 1);
            movementLocked = movementLocks > 0;
            Publish();
        });
    }

    private void Publish()
    {
        if (disposed || !IsServer || !IsSpawned) return;
        revision++;
        Present();
        Controller.JolteonSnapshotRPC(CapturePresentationState());
    }

    public override string CapturePresentationState() => JsonUtility.ToJson(new PresentationState
    {
        Revision = revision,
        PassiveCharge = IsServer ? passiveCharge.Amount : observedPassiveCharge,
        OverchargeUntil = IsServer ? Now + Math.Max(0f, passiveCharge.OverchargeUntil - Time.time) : observedOverchargeUntil,
        AttackCharge = attackCharge,
        Evolved = evolved,
        MovementLocked = movementLocked,
        DashActive = dashActive,
        RecastAvailable = recastAvailable,
        RecastMove = (int)recastMove,
        RecastUntil = recastUntil,
        RecastEmpowered = recastEmpowered,
        UniteCastId = uniteCastId,
        TraversalCastId = traversalCastId,
        TraversalSequence = traversalSequence,
        TraversalActive = traversalActive
    });

    public override void ApplyPresentationState(string state)
    {
        if (disposed || IsServer || string.IsNullOrEmpty(state)) return;
        PresentationState snapshot;
        try { snapshot = JsonUtility.FromJson<PresentationState>(state); }
        catch (ArgumentException) { return; }
        if (snapshot == null || snapshot.Revision < revision || float.IsNaN(snapshot.PassiveCharge)
            || float.IsInfinity(snapshot.PassiveCharge) || double.IsNaN(snapshot.OverchargeUntil)
            || double.IsInfinity(snapshot.OverchargeUntil) || double.IsNaN(snapshot.RecastUntil)
            || double.IsInfinity(snapshot.RecastUntil)) return;
        revision = snapshot.Revision;
        observedPassiveCharge = Mathf.Clamp(snapshot.PassiveCharge, 0f, 100f);
        observedOverchargeUntil = snapshot.OverchargeUntil;
        attackCharge = (byte)Mathf.Clamp(snapshot.AttackCharge, 0, 2);
        evolved = snapshot.Evolved;
        movementLocked = snapshot.MovementLocked;
        dashActive = snapshot.DashActive;
        recastAvailable = snapshot.RecastAvailable;
        recastMove = (AvailableMoves)snapshot.RecastMove;
        recastUntil = snapshot.RecastUntil;
        recastEmpowered = snapshot.RecastEmpowered;
        uniteCastId = snapshot.UniteCastId;
        traversalCastId = snapshot.TraversalCastId;
        traversalSequence = snapshot.TraversalSequence;
        traversalActive = snapshot.TraversalActive;
        Present();
    }

    private void Present()
    {
        if (Actor == null) return;
        if ((IsOwner || IsServer) && Actor.PlayerMovement != null && appliedMovement != movementLocked)
        {
            if (movementLocked) Actor.PlayerMovement.AddMovementRestriction();
            else Actor.PlayerMovement.RemoveMovementRestriction();
            appliedMovement = movementLocked;
        }
        if (Actor.HPBar != null && evolved)
        {
            Actor.HPBar.ShowGenericGuage(true);
            Actor.HPBar.UpdateGenericGuageValue(PassiveAmount, 100f);
        }
        if (Actor.AnimationManager != null && Actor.AnimationManager.Animator != null
            && Mathf.Abs(PassiveAmount / 100f - Actor.AnimationManager.Animator.GetFloat("PassiveAmount")) > 0.03f)
            Actor.AnimationManager.Animator.SetFloat("PassiveAmount", PassiveAmount / 100f);
        if (IsOwner && Actor.IsLocalHuman && BattleUIManager.instance != null)
        {
            double until = RecastAvailable ? recastUntil : 0d;
            if (shownRecastUntil != until)
            {
                shownRecastUntil = until;
                BattleUIManager.instance.ShowMoveSecondaryCooldown(0, (float)Math.Max(0d, until - Now));
            }
        }
    }

    private void OnDeath(DamageInfo damage)
    {
        if (!IsServer || disposed) return;
        passiveCharge.Clear();
        nextDamageCharge.Clear();
        chargeBudget = budgetUntil = nextReadyVisual = 0f;
        attackCharge = 0;
        basicRecovery?.Dispose();
        basicRecovery = null;
        CancelEffects();
        Publish();
    }

    private void CancelEffects()
    {
        foreach (JoltEffectRequest request in new List<JoltEffectRequest>(effectRequests)) request.Cancel();
        effectRequests.Clear();
        persistentEffects.Clear();
        foreach (JoltSwiftProjectile projectile in swiftProjectiles)
            if (projectile != null && projectile.IsSpawned) projectile.NetworkObject.Despawn(true);
        swiftProjectiles.Clear();
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        UnbindEvents();
        basicRecovery?.Dispose();
        basicRecovery = null;
        CancelEffects();
        lifetime.Dispose();
        prefabs.Clear();
        movementLocked = false;
        Present();
        if (Actor != null && Actor.HPBar != null) Actor.HPBar.ShowGenericGuage(false);
    }
}
