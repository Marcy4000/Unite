using System;
using System.Collections;
using System.Collections.Generic;
using Unite.Combat;
using UnityEngine;
using UnityEngine.AddressableAssets;

public sealed class SylveonCharacterRuntime : CombatCharacterRuntime
{
    [Serializable]
    public sealed class PresentationState
    {
        public long Revision;
        public byte Charge;
        public byte PassiveStacks;
        public double PassiveUntil;
        public bool MovementLocked;
        public bool HyperVoiceActive;
        public double HyperVoiceUntil;
        public Vector3 HyperVoiceDirection;
    }

    private readonly CombatScope lifetime = new CombatScope();
    private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    private readonly HashSet<string> loading = new HashSet<string>();
    private readonly Dictionary<AvailableMoves, PrefabMoveEffect> activeEffects = new Dictionary<AvailableMoves, PrefabMoveEffect>();
    private readonly HashSet<SylvSwiftProjectile> activeSwiftProjectiles = new HashSet<SylvSwiftProjectile>();
    private readonly List<IDisposable> presentationLocks = new List<IDisposable>();

    private Pokemon boundPokemon;
    private byte charge;
    private double chargeUntil;
    private byte passiveStacks;
    private double passiveUntil;
    private long stateRevision;
    private int movementLocks;
    private bool movementLocked, appliedMovement, disposed;
    private bool hyperVoiceActive, shownHyperVoice;
    private double hyperVoiceUntil;
    private Vector3 hyperVoiceDirection = Vector3.forward;
    private int shownCharge = -1;
    private SylvBasicAtk shownAttack;
    private CombatRoutine basicRecovery;
    private CombatRoutine dashRoutine;
    private WallCrossingTraversal dashTraversal;
    private Vector3 dashDestination;

    public SylveonCharacterRuntime(PlayerManager actor) : base(actor)
    {
        lifetime.Own(new CombatCallbackLease(() => basicRecovery?.Dispose()));
        lifetime.Own(new CombatCallbackLease(() => StopDashPresentation(false, Vector3.zero)));
        BindEvents();
    }

    private double Now => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    private bool IsSylveon => Pokemon != null && Pokemon.BaseStats != null && Pokemon.BaseStats.Passive == AvailablePassives.SylveonPassive;

    public bool Evolved => Pokemon != null && Pokemon.CurrentLevel >= 3;
    public byte Charge => charge;
    public byte PassiveStacks => passiveStacks;
    public double PassiveUntil => passiveUntil;
    public bool MovementLocked => movementLocked;
    public bool HyperVoiceActive => hyperVoiceActive;
    public override bool CanStartBasicAttack => !hyperVoiceActive;

    public void FillCharge()
    {
        if (!IsServer || disposed) return;
        charge = 2;
        chargeUntil = Now + SylvMoveRules.BasicExpiry;
        Publish();
    }

    public void ResetBasicCharge()
    {
        if (!IsServer || disposed) return;
        basicRecovery?.Dispose();
        basicRecovery = null;
        charge = 0;
        chargeUntil = 0;
        Publish();
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

    internal IDisposable AcquireHyperVoiceChannel(Vector3 direction, double until)
    {
        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        hyperVoiceDirection = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward;
        hyperVoiceActive = true;
        hyperVoiceUntil = until;
        Publish();
        return new CombatCallbackLease(() =>
        {
            hyperVoiceActive = false;
            hyperVoiceUntil = 0;
            Publish();
        });
    }

    internal void RegisterEffect(AvailableMoves moveId, PrefabMoveEffect effect)
    {
        if (effect == null) return;
        if (activeEffects.TryGetValue(moveId, out PrefabMoveEffect previous) && previous != null && previous != effect && previous.IsSpawned)
            previous.Despawn();
        activeEffects[moveId] = effect;
    }

    internal void TrackSwiftProjectile(SylvSwiftProjectile projectile)
    {
        if (projectile != null)
            activeSwiftProjectiles.Add(projectile);
    }

    internal void BeginDash(Vector3 direction, float distance, float duration)
    {
        if (!IsServer || !IsSpawned) return;
        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        Vector3 unit = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward;
        Controller.SylveonDashRPC(transform.position, transform.position + unit * distance, Now, duration, true, false);
    }

    internal void EndDash(bool completed)
    {
        if (IsServer && IsSpawned)
            Controller.SylveonDashRPC(Vector3.zero, Vector3.zero, 0, 0, false, completed);
    }

    public void ReceiveDash(Vector3 origin, Vector3 destination, double started, float duration, bool active, bool completed)
    {
        if (disposed || (!IsOwner && !IsServer)) return;
        StopDashPresentation(completed, dashDestination);
        if (!active || duration <= 0f || !CombatTargeting.Finite(origin) || !CombatTargeting.Finite(destination)) return;
        dashDestination = destination;
        dashRoutine = new CombatRoutine(Actor, DriveDash(origin, destination, started, duration));
    }

    private IEnumerator DriveDash(Vector3 origin, Vector3 destination, double started, float duration)
    {
        var traversal = new WallCrossingTraversal();
        dashTraversal = traversal;
        traversal.Begin(Actor.PlayerMovement);
        while (IsSpawned && Pokemon != null && Pokemon.CurrentHp > 0 && !Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions))
        {
            float progress = Mathf.Clamp01((float)((Now - started) / Math.Max(0.01, duration)));
            traversal.Move(Vector3.Lerp(origin, destination, progress) - transform.position);
            if (progress >= 1f) break;
            yield return null;
        }
        if (IsSpawned && Pokemon != null && Pokemon.CurrentHp > 0) traversal.End();
        else traversal.Cancel();
        dashTraversal = null;
    }

    private void StopDashPresentation(bool completed, Vector3 destination)
    {
        dashRoutine?.Dispose();
        dashRoutine = null;
        if (dashTraversal == null) return;
        if (IsSpawned && Pokemon != null && Pokemon.CurrentHp > 0)
        {
            if (completed && !Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions) && CombatTargeting.Finite(destination))
                dashTraversal.Move(destination - transform.position);
            dashTraversal.End();
        }
        else
        {
            dashTraversal.Cancel();
        }
        dashTraversal = null;
    }

    public override bool PrepareCast(CombatExecutionContext context)
    {
        if (disposed || !IsSylveon || hyperVoiceActive) return false;
        if (context.MoveId == AvailableMoves.SylvMysticalCringe && Pokemon.HasStatusEffect(StatusType.Immobilized))
            return false;
        return true;
    }

    public override bool RevalidateCast(CombatExecutionContext context) => PrepareCast(context);

    public override CombatExecution CreateExecution(CombatExecutionContext context) => new SylveonExecution(context, this);

    public override void Tick()
    {
        if (disposed || !IsSpawned || Pokemon == null || !IsSylveon) return;
        if (IsServer)
        {
            BindEvents();
            activeSwiftProjectiles.RemoveWhere(projectile => projectile == null || !projectile.IsSpawned);
            EnsurePrefab(SylvMoveRules.BasicAttackPath);
            EnsurePrefab(SylvMoveRules.BoostedAttackPath);

            if (Pokemon.CurrentHp <= 0)
            {
                bool changed = false;
                if (charge != 0)
                {
                    charge = 0;
                    chargeUntil = 0;
                    changed = true;
                }
                if (passiveStacks != 0)
                {
                    passiveStacks = 0;
                    passiveUntil = 0;
                    ApplyPassiveStacks();
                    changed = true;
                }
                if (changed) Publish();
            }
            else
            {
                bool changed = false;
                if (charge != 0 && Now >= chargeUntil)
                {
                    charge = 0;
                    chargeUntil = 0;
                    changed = true;
                }
                if (passiveStacks != 0 && Now >= passiveUntil)
                {
                    passiveStacks = 0;
                    passiveUntil = 0;
                    ApplyPassiveStacks();
                    changed = true;
                }
                if (changed) Publish();
            }
        }
        Present();
    }

    public override void LateTick()
    {
        if (disposed || !IsSpawned || Pokemon == null || !IsSylveon) return;
        if (hyperVoiceActive && hyperVoiceDirection.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(hyperVoiceDirection);
    }

    private void BindEvents()
    {
        if (boundPokemon == Pokemon) return;
        UnbindEvents();
        boundPokemon = Pokemon;
        if (boundPokemon != null)
        {
            boundPokemon.OnDamageTaken += OnDamageTaken;
            boundPokemon.OnDamageDealt += OnDamageDealt;
            boundPokemon.OnDeath += OnDeath;
            boundPokemon.OnEvolution += OnEvolution;
        }
    }

    private void UnbindEvents()
    {
        if (boundPokemon == null) return;
        boundPokemon.OnDamageTaken -= OnDamageTaken;
        boundPokemon.OnDamageDealt -= OnDamageDealt;
        boundPokemon.OnDeath -= OnDeath;
        boundPokemon.OnEvolution -= OnEvolution;
        boundPokemon = null;
    }

    private void OnDamageDealt(ulong target, DamageInfo damage) => OnDamage(damage);

    private void OnDamageTaken(DamageInfo damage) => OnDamage(damage);

    private void OnDamage(DamageInfo damage)
    {
        if (!IsServer || disposed || Pokemon == null || Pokemon.CurrentHp <= 0 || !SylvMoveRules.CountsForPassive(damage))
            return;
        int current = Now < passiveUntil ? passiveStacks : 0;
        passiveStacks = (byte)Mathf.Min(SylvMoveRules.PassiveMaxStacks, current + 1);
        passiveUntil = Now + SylvMoveRules.PassiveStackDuration;
        ApplyPassiveStacks();
        Publish();
    }

    private void OnEvolution()
    {
        if (!IsServer || disposed || Pokemon == null) return;
        if (passiveStacks > 0 && Now < passiveUntil)
        {
            ApplyPassiveStacks();
            Publish();
        }
    }

    private void OnDeath(DamageInfo damage)
    {
        if (!IsServer || disposed) return;
        basicRecovery?.Dispose();
        basicRecovery = null;
        charge = 0;
        chargeUntil = 0;
        passiveStacks = 0;
        passiveUntil = 0;
        ApplyPassiveStacks();
        DespawnActiveEffects();
        Publish();
    }

    private void ApplyPassiveStacks()
    {
        if (Pokemon == null) return;
        Pokemon.RemoveAllStatChangeWithIDRPC(SylvMoveRules.PassiveAttack);
        Pokemon.RemoveAllStatChangeWithIDRPC(SylvMoveRules.PassiveDefense);
        float remaining = (float)(passiveUntil - Now);
        if (passiveStacks == 0 || remaining <= 0f) return;
        short bonus = (short)(passiveStacks * SylvMoveRules.PassiveBonusPerStack);
        Pokemon.AddStatChange(new StatChange(bonus, Stat.SpAttack, remaining, true, true, true, SylvMoveRules.PassiveAttack));
        if (Evolved)
            Pokemon.AddStatChange(new StatChange(bonus, Stat.SpDefense, remaining, true, true, true, SylvMoveRules.PassiveDefense));
    }

    private void EnsurePrefab(string path)
    {
        if (prefabs.ContainsKey(path) || loading.Contains(path) || Controller.Combat == null) return;
        lifetime.Own(new CombatRoutine(Actor, LoadPrefab(path)));
    }

    private IEnumerator LoadPrefab(string path)
    {
        loading.Add(path);
        yield return Controller.Combat.Spawner.Load(path, lifetime, loaded =>
        {
            if (loaded != null && loaded.GetComponent<HomingProjectile>() != null && !disposed)
                prefabs[path] = loaded;
        });
        loading.Remove(path);
    }

    public void PerformBasicAttack(Pokemon target)
    {
        if (!IsServer || disposed || !IsSylveon || hyperVoiceActive || Pokemon.CurrentHp <= 0
            || !SylvMoveRules.CanHit(Actor, target)
            || !CombatTargeting.WithinMeleeRange(transform.position, target.transform.position, SylvMoveRules.BasicRange))
            return;

        byte previous = charge;
        bool boosted = previous == 2;
        bool evolved = Evolved;
        string path = boosted && evolved ? SylvMoveRules.BoostedAttackPath : SylvMoveRules.BasicAttackPath;
        DamageInfo damage = SylvMoveRules.BasicDamage(Pokemon.NetworkObjectId, boosted);

        charge = (byte)((previous + 1) % 3);
        chargeUntil = Now + SylvMoveRules.BasicExpiry;

        if (prefabs.TryGetValue(path, out GameObject cached) && cached != null)
            SpawnBasicProjectile(cached, path, target, damage);
        else
            lifetime.Own(new CombatRoutine(Actor, LoadAndSpawnBasic(path, target, damage)));

        string animation = SylvMoveRules.BasicAnimation(previous, evolved);
        basicRecovery?.Dispose();
        basicRecovery = new CombatRoutine(Actor, BasicRecovery(target, animation));
        Publish();
    }

    private IEnumerator LoadAndSpawnBasic(string path, Pokemon target, DamageInfo damage)
    {
        GameObject loadedPrefab = null;
        yield return Controller.Combat.Spawner.Load(path, lifetime, loaded => loadedPrefab = loaded);
        if (disposed || !IsServer || !IsSpawned || Pokemon == null || Pokemon.CurrentHp <= 0 || !SylvMoveRules.CanHit(Actor, target))
            yield break;
        if (loadedPrefab != null && loadedPrefab.GetComponent<HomingProjectile>() != null)
        {
            prefabs[path] = loadedPrefab;
            SpawnBasicProjectile(loadedPrefab, path, target, damage);
        }
    }

    private void SpawnBasicProjectile(GameObject prefab, string path, Pokemon target, DamageInfo damage)
    {
        if (disposed || !IsServer || !IsSpawned || prefab == null || !SylvMoveRules.CanHit(Actor, target)) return;
        GameObject spawned = Controller.Combat.Spawner.Spawn(prefab, transform.position, Quaternion.identity, lifetime, false);
        if (spawned == null) return;
        HomingProjectile projectile = spawned.GetComponent<HomingProjectile>();
        if (projectile == null) return;
        projectile.HoldAsset(Addressables.LoadAssetAsync<GameObject>(path));
        projectile.SetTargetRPC(target.NetworkObjectId, damage);
    }

    private IEnumerator BasicRecovery(Pokemon target, string animation)
    {
        float duration = 0.5f * Controller.GetAtkSpeedCooldown();
        Vector3 direction = target != null ? target.transform.position - transform.position : transform.forward;
        Controller.PresentServerAttackRPC(true, direction, animation, duration);
        try
        {
            yield return new WaitForSeconds(duration);
        }
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
        if (!IsServer || !IsSpawned || Pokemon == null || Pokemon.CurrentHp <= 0 || Pokemon.HasAnyStatusEffect(SylvMoveRules.Interrupts))
            return;
        Actor.AnimationManager?.RecoverAnimationRPC(animation, SylvMoveRules.IdleAnimation(Evolved), 0.06f);
    }

    private void Publish()
    {
        if (!disposed && IsServer && IsSpawned)
        {
            stateRevision++;
            PresentMovement();
            Controller.SylveonSnapshotRPC(
                charge,
                passiveStacks,
                passiveUntil,
                movementLocked,
                hyperVoiceActive,
                hyperVoiceUntil,
                hyperVoiceDirection,
                stateRevision);
        }
    }

    public override string CapturePresentationState() => JsonUtility.ToJson(new PresentationState
    {
        Revision = stateRevision,
        Charge = charge,
        PassiveStacks = passiveStacks,
        PassiveUntil = passiveUntil,
        MovementLocked = movementLocked,
        HyperVoiceActive = hyperVoiceActive,
        HyperVoiceUntil = hyperVoiceUntil,
        HyperVoiceDirection = hyperVoiceDirection
    });

    public override void ApplyPresentationState(string state)
    {
        if (disposed || IsServer || string.IsNullOrEmpty(state)) return;
        PresentationState snapshot;
        try { snapshot = JsonUtility.FromJson<PresentationState>(state); }
        catch (ArgumentException) { return; }
        if (snapshot == null || double.IsNaN(snapshot.PassiveUntil) || double.IsInfinity(snapshot.PassiveUntil)
            || double.IsNaN(snapshot.HyperVoiceUntil) || double.IsInfinity(snapshot.HyperVoiceUntil)
            || !CombatTargeting.Finite(snapshot.HyperVoiceDirection))
            return;
        ReceiveSnapshot(
            snapshot.Charge,
            snapshot.PassiveStacks,
            snapshot.PassiveUntil,
            snapshot.MovementLocked,
            snapshot.HyperVoiceActive,
            snapshot.HyperVoiceUntil,
            snapshot.HyperVoiceDirection,
            snapshot.Revision);
    }

    public void ReceiveSnapshot(
        byte attackCharge,
        byte stacks,
        double stacksUntil,
        bool movement,
        bool screaming,
        double screamUntil,
        Vector3 screamDirection,
        long revision)
    {
        if (disposed || revision < stateRevision) return;
        stateRevision = revision;
        charge = (byte)Mathf.Clamp(attackCharge, 0, 2);
        passiveStacks = (byte)Mathf.Clamp(stacks, 0, SylvMoveRules.PassiveMaxStacks);
        passiveUntil = stacksUntil;
        movementLocked = movement;
        hyperVoiceActive = screaming;
        hyperVoiceUntil = screamUntil;
        if (CombatTargeting.Finite(screamDirection) && screamDirection.sqrMagnitude > 0.001f)
            hyperVoiceDirection = new Vector3(screamDirection.x, 0f, screamDirection.z).normalized;
        Present();
    }

    private void Present()
    {
        PresentMovement();

        if (Actor.HPBar != null)
        {
            if (hyperVoiceActive)
            {
                shownHyperVoice = true;
                Actor.HPBar.ShowGenericGuage(true);
                float remaining = Mathf.Clamp((float)(hyperVoiceUntil - Now), 0f, SylvMoveRules.HyperVoiceDuration);
                Actor.HPBar.UpdateGenericGuageValue(remaining, SylvMoveRules.HyperVoiceDuration);
            }
            else if (shownHyperVoice)
            {
                shownHyperVoice = false;
                Actor.HPBar.ShowGenericGuage(false);
            }
        }

        if (!IsOwner) return;

        var attack = Controller.BasicAttack as SylvBasicAtk;
        if (shownCharge != charge || shownAttack != attack)
        {
            shownCharge = charge;
            shownAttack = attack;
            attack?.ApplySnapshot(charge);
        }

        if (hyperVoiceActive && presentationLocks.Count == 0)
        {
            presentationLocks.Add(Controller.AcquireMoveStatus(0, ActionStatusType.Disabled));
            presentationLocks.Add(Controller.AcquireMoveStatus(1, ActionStatusType.Disabled));
            presentationLocks.Add(Controller.AcquireMoveStatus(2, ActionStatusType.Disabled));
            presentationLocks.Add(Controller.BasicAttackStatus.AcquireStatus(ActionStatusType.Disabled));
            presentationLocks.Add(Actor.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
        }
        else if (!hyperVoiceActive && presentationLocks.Count > 0)
        {
            foreach (IDisposable lease in presentationLocks) lease.Dispose();
            presentationLocks.Clear();
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

    private void DespawnActiveEffects()
    {
        foreach (PrefabMoveEffect effect in activeEffects.Values)
        {
            if (effect != null && effect.IsSpawned)
                effect.Despawn();
        }
        activeEffects.Clear();

        foreach (SylvSwiftProjectile projectile in activeSwiftProjectiles)
        {
            if (projectile != null && projectile.IsSpawned)
                projectile.DespawnSelf();
        }
        activeSwiftProjectiles.Clear();
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        UnbindEvents();
        lifetime.Dispose();
        DespawnActiveEffects();
        movementLocked = false;
        hyperVoiceActive = false;
        PresentMovement();
        if (shownHyperVoice && Actor?.HPBar != null)
        {
            shownHyperVoice = false;
            Actor.HPBar.ShowGenericGuage(false);
        }
        foreach (IDisposable lease in presentationLocks) lease.Dispose();
        presentationLocks.Clear();
        prefabs.Clear();
        loading.Clear();
    }
}
