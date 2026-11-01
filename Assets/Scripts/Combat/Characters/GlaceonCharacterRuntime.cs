using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unite.Combat;

public sealed class GlaceonCharacterRuntime : CombatCharacterRuntime
{
    [Serializable]
    public sealed class PresentationState
    {
        public long Revision;
        public byte Crystals, Charge, Mode;
        public double CrystalUntil;
        public bool SpearLocked, ShardLocked, MovementLocked, Flying;
    }

    public const string BasicPath = "Assets/Prefabs/Objects/BasicAtk/GlaceonBasicAtk.prefab";
    public const string StarPath = "Assets/Prefabs/Objects/BasicAtk/CinderBasicAtk.prefab";
    public const string TailPath = "Assets/Prefabs/Objects/Moves/Glaceon/GlaceonTailWhip.prefab";
    public const string HolderPath = "Assets/Prefabs/Objects/Passives/Glaceon/IciclesHolder.prefab";
    private readonly CombatScope lifetime = new CombatScope();
    private readonly Dictionary<string, GameObject> projectiles = new Dictionary<string, GameObject>();
    private readonly HashSet<string> loading = new HashSet<string>();
    private readonly List<IDisposable> presentationLocks = new List<IDisposable>();
    private IciclesHolder holder;
    private byte crystals, charge, mode;
    private double crystalUntil, chargeUntil;
    private long stateRevision;
    private bool spearLocked, shardLocked, disposed;
    private int movementLocks, flightLocks;
    private bool movementLocked, flying, appliedMovement, appliedFlight, previousFlight;
    private CombatRoutine basicRecovery;
    private CombatRoutine dashRoutine;
    private WallCrossingTraversal dashTraversal;
    private byte shownLocks;
    private int shownCharge = -1;
    private byte shownMode = byte.MaxValue;
    private GlaceBasicAtk shownAttack;
    internal GlaceonExecution Swift, Shard, Wind;

    public GlaceonCharacterRuntime(PlayerManager actor) : base(actor)
    {
        lifetime.Own(new GlaceonCleanup(() => basicRecovery?.Dispose()));
        lifetime.Own(new GlaceonCleanup(() => StopDashPresentation(false, Vector3.zero)));
    }
    private double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    public byte Crystals => crystals;
    public byte Charge => charge;
    public byte BasicMode => mode;
    public float CrystalRemaining => Mathf.Clamp((float)(crystalUntil - Now), 0, 5);
    public bool Evolved => Pokemon != null && Pokemon.CurrentLevel >= 3;
    public override bool CanStartBasicAttack => !spearLocked;

    public static bool CanHit(PlayerManager actor, Pokemon target)
    {
        if (actor == null || target == null || !target.IsSpawned || target.CurrentHp <= 0
            || target == actor.Pokemon || target.HasStatusEffect(StatusType.Invincible)
            || target.HasStatusEffect(StatusType.Untargetable)) return false;
        return !target.TeamMember.IsOnSameTeam(actor.CurrentTeam);
    }

    public static byte ClampCrystals(int count) => (byte)Mathf.Clamp(count, 0, 8);

    public void SetCrystals(int count)
    {
        if (!IsServer || disposed || !Evolved || Pokemon.CurrentHp <= 0) return;
        byte previous = crystals;
        crystals = ClampCrystals(count);
        if (crystals >= previous) crystalUntil = Now + 5;
        Publish();
    }

    public void ResetCrystalTimer()
    {
        if (!IsServer || disposed || !Evolved) return;
        crystalUntil = Now + 5;
        Publish();
    }

    public void FillCharge()
    {
        if (!IsServer || disposed) return;
        charge = 2;
        chargeUntil = Now + 4.5;
        Publish();
    }

    internal void SetMode(byte value)
    {
        if (!IsServer || disposed) return;
        mode = (byte)Mathf.Clamp(value, 0, 2);
        Publish();
    }

    internal IDisposable LockSpear()
    {
        spearLocked = true;
        Publish();
        return new GlaceonCleanup(() => { spearLocked = false; Publish(); });
    }

    internal IDisposable LockShard()
    {
        shardLocked = true;
        Publish();
        return new GlaceonCleanup(() => { shardLocked = false; Publish(); });
    }

    internal IDisposable AcquireMovementLock()
    {
        movementLocks++;
        movementLocked = true;
        Publish();
        return new GlaceonCleanup(() => { movementLocked = --movementLocks > 0; Publish(); });
    }

    internal IDisposable AcquireFlight()
    {
        flightLocks++;
        flying = true;
        Publish();
        return new GlaceonCleanup(() => { flying = --flightLocks > 0; Publish(); });
    }

    internal void BeginDash(Vector3 direction)
    {
        if (IsServer && IsSpawned)
            Controller.GlaceonDashRPC(transform.position, transform.position + direction * 5, Now, true, false);
    }

    internal void EndDash(bool completed)
    {
        if (IsServer && IsSpawned) Controller.GlaceonDashRPC(Vector3.zero, Vector3.zero, 0, false, completed);
    }

    private Vector3 dashDestination;
    public void ReceiveDash(Vector3 origin, Vector3 destination, double started, bool active, bool completed)
    {
        if (disposed || (!IsOwner && !IsServer)) return;
        StopDashPresentation(completed, dashDestination);
        if (!active) return;
        dashDestination = destination;
        dashRoutine = new CombatRoutine(Actor, DriveDash(origin, destination, started));
    }

    private IEnumerator DriveDash(Vector3 origin, Vector3 destination, double started)
    {
        var traversal = new WallCrossingTraversal();
        dashTraversal = traversal;
        traversal.Begin(Actor.PlayerMovement);
        while (IsSpawned && Pokemon.CurrentHp > 0 && !Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions))
        {
            float progress = Mathf.Clamp01((float)((Now - started) / 0.2));
            traversal.Move(Vector3.Lerp(origin, destination, progress) - transform.position);
            if (progress >= 1) break;
            yield return null;
        }
        if (IsSpawned && Pokemon.CurrentHp > 0) traversal.End(); else traversal.Cancel();
        dashTraversal = null;
    }

    private void StopDashPresentation(bool completed, Vector3 destination)
    {
        dashRoutine?.Dispose();
        dashRoutine = null;
        if (dashTraversal == null) return;
        if (IsSpawned && Pokemon.CurrentHp > 0)
        {
            if (completed && !Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions)) dashTraversal.Move(destination - transform.position);
            dashTraversal.End();
        }
        else dashTraversal.Cancel();
        dashTraversal = null;
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

    public override bool PrepareCast(CombatExecutionContext context) => !disposed && !spearLocked
        && !(shardLocked && context.MoveId == AvailableMoves.GlaceIceShard);
    public override bool RevalidateCast(CombatExecutionContext context) => PrepareCast(context);
    public override CombatExecution CreateExecution(CombatExecutionContext context) => new GlaceonExecution(context, this);

    public override void Tick()
    {
        if (disposed || !IsSpawned || Pokemon == null) return;
        if (IsServer)
        {
            EnsureProjectile(BasicPath);
            EnsureProjectile(StarPath);
            if (Evolved && holder == null && !loading.Contains(HolderPath))
                lifetime.Own(new CombatRoutine(Actor, LoadHolder()));
            if (Pokemon.CurrentHp <= 0)
            {
                if (crystals != 0 || charge != 0 || mode != 0)
                {
                    crystals = charge = mode = 0;
                    crystalUntil = chargeUntil = 0;
                    Publish();
                }
            }
            else
            {
                if (charge != 0 && Now >= chargeUntil) { charge = 0; Publish(); }
                if (crystals != 0 && Now >= crystalUntil) { crystals = 0; Publish(); }
            }
            if (holder != null) { holder.transform.position = transform.position; holder.SetCount(crystals); }
        }
        Present();
    }

    private void EnsureProjectile(string path)
    {
        if (projectiles.ContainsKey(path) || loading.Contains(path)) return;
        lifetime.Own(new CombatRoutine(Actor, LoadProjectile(path)));
    }

    private IEnumerator LoadProjectile(string path)
    {
        loading.Add(path);
        yield return Controller.Combat.Spawner.Load(path, lifetime, prefab =>
        {
            if (prefab != null && prefab.GetComponent<HomingProjectile>() != null && !disposed) projectiles[path] = prefab;
        });
        loading.Remove(path);
    }

    private IEnumerator LoadHolder()
    {
        loading.Add(HolderPath);
        GameObject prefab = null;
        yield return Controller.Combat.Spawner.Load(HolderPath, lifetime, loaded => prefab = loaded);
        if (!disposed && prefab != null && IsSpawned)
        {
            var spawned = Controller.Combat.Spawner.Spawn(prefab, transform.position, Quaternion.identity, lifetime);
            holder = spawned != null ? spawned.GetComponent<IciclesHolder>() : null;
            if (holder != null) holder.Follow(Actor.NetworkObjectId);
        }
        loading.Remove(HolderPath);
    }

    internal bool Launch(Pokemon target, DamageInfo damage, string path)
    {
        if (!IsServer || disposed || !CanHit(Actor, target) || !projectiles.TryGetValue(path, out var prefab)) return false;
        return LaunchPrepared(target, damage, prefab, lifetime);
    }

    internal bool LaunchPrepared(Pokemon target, DamageInfo damage, GameObject prefab, CombatScope scope)
    {
        if (!IsServer || disposed || !CanHit(Actor, target) || prefab == null) return false;
        var spawned = Controller.Combat.Spawner.Spawn(prefab, transform.position, Quaternion.identity, scope);
        if (spawned == null) return false;
        var projectile = spawned.GetComponent<HomingProjectile>();
        if (projectile == null) return false;
        projectile.SetTargetRPC(target.NetworkObjectId, damage);
        return true;
    }

    public void PerformBasicAttack(Pokemon target)
    {
        float reach = mode == 2 ? 8 : 7;
        if (!IsServer || disposed || spearLocked || !CanHit(Actor, target)
            || !CombatTargeting.WithinMeleeRange(transform.position, target.transform.position, reach)) return;
        if (!projectiles.ContainsKey(BasicPath) || (Swift != null || mode == 1) && !projectiles.ContainsKey(StarPath)) return;
        byte previous = charge;
        if (mode == 2)
        {
            Wind?.ConsumeWind(target);
            return;
        }
        if (mode == 1)
        {
            if (!Launch(target, new DamageInfo(Pokemon.NetworkObjectId, 0.52f, 6, 100, DamageType.Special, DamageProprieties.CanCrit), StarPath)) return;
            SetCrystals(crystals + 2);
        }
        else
        {
            var flags = DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit;
            var damage = previous == 2
                ? new DamageInfo(Pokemon.NetworkObjectId, 0.47f, 5, 90, DamageType.Special, flags)
                : new DamageInfo(Pokemon.NetworkObjectId, 0.39f, 4, 70, DamageType.Special, flags);
            if (!Launch(target, damage, BasicPath)) return;
            if (previous == 2) SetCrystals(crystals + 2);
            charge = (byte)((previous + 1) % 3);
            chargeUntil = Now + 4.5;
        }
        Swift?.ShootStars(target, mode == 0 ? previous == 2 ? 2 : 1 : charge == 0 ? 2 : 1);
        string animation = mode == 1 ? "ani_spell1b_bat_0471" : Pokemon.CurrentLevel >= 3
            ? $"ani_atk{previous + 1}_bat_0471" : $"ani_atk{previous + 4}_bat_0133";
        basicRecovery?.Dispose();
        basicRecovery = new CombatRoutine(Actor, BasicRecovery(target, animation));
        Publish();
    }

    private IEnumerator BasicRecovery(Pokemon target, string animation)
    {
        float duration = 0.5f * Controller.GetAtkSpeedCooldown();
        Controller.PresentServerAttackRPC(true, target.transform.position - transform.position, animation, duration);
        try { yield return new WaitForSeconds(duration); }
        finally
        {
            if (Actor != null && Actor.IsSpawned)
            {
                Controller.PresentServerAttackRPC(false, Vector3.zero, "");
                RecoverBasicAnimation(animation);
            }
        }
    }

    internal void RecoverBasicAnimation(string animation)
    {
        if (!IsServer || !IsSpawned || Pokemon.CurrentHp <= 0 || Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions)) return;
        Actor.AnimationManager?.RecoverAnimationRPC(animation, Evolved ? "ani_idle_bat_0471" : "ani_idle_bat_0133", 0.06f);
    }

    private void Publish()
    {
        if (!disposed && IsServer && IsSpawned)
        {
            stateRevision++;
            PresentMovement();
            Controller.GlaceonSnapshotRPC(crystals, crystalUntil, charge, mode, spearLocked, shardLocked, movementLocked, flying, stateRevision);
        }
    }

    public override string CapturePresentationState() => JsonUtility.ToJson(new PresentationState
    {
        Revision = stateRevision,
        Crystals = crystals,
        CrystalUntil = crystalUntil,
        Charge = charge,
        Mode = mode,
        SpearLocked = spearLocked,
        ShardLocked = shardLocked,
        MovementLocked = movementLocked,
        Flying = flying
    });

    public override void ApplyPresentationState(string state)
    {
        if (disposed || IsServer || string.IsNullOrEmpty(state)) return;
        PresentationState snapshot;
        try { snapshot = JsonUtility.FromJson<PresentationState>(state); }
        catch (ArgumentException) { return; }
        if (snapshot == null || double.IsNaN(snapshot.CrystalUntil) || double.IsInfinity(snapshot.CrystalUntil)) return;
        ReceiveSnapshot(snapshot.Crystals, snapshot.CrystalUntil, snapshot.Charge, snapshot.Mode,
            snapshot.SpearLocked, snapshot.ShardLocked, snapshot.MovementLocked, snapshot.Flying, snapshot.Revision);
    }

    public void ReceiveSnapshot(byte count, double expiresAt, byte attackCharge, byte attackMode, bool spear, bool shard, bool movement, bool flight, long revision)
    {
        if (disposed || revision < stateRevision) return;
        stateRevision = revision;
        crystals = ClampCrystals(count);
        crystalUntil = expiresAt;
        charge = (byte)Mathf.Clamp(attackCharge, 0, 2);
        mode = (byte)Mathf.Clamp(attackMode, 0, 2);
        spearLocked = spear;
        shardLocked = shard;
        movementLocked = movement;
        flying = flight;
        Present();
    }

    private void Present()
    {
        PresentMovement();
        if (!IsOwner) return;
        var attack = Controller.BasicAttack as GlaceBasicAtk;
        if (shownCharge != charge || shownMode != mode || shownAttack != attack)
        {
            shownCharge = charge; shownMode = mode;
            shownAttack = attack;
            attack?.ApplySnapshot(charge, mode);
        }
        byte locks = (byte)((spearLocked ? 1 : 0) | (shardLocked ? 2 : 0));
        if (shownLocks == locks) return;
        foreach (var lease in presentationLocks) lease.Dispose();
        presentationLocks.Clear();
        shownLocks = locks;
        if (spearLocked)
        {
            presentationLocks.Add(Controller.AcquireMoveStatus(1, ActionStatusType.Disabled));
            presentationLocks.Add(Controller.AcquireMoveStatus(2, ActionStatusType.Disabled));
            presentationLocks.Add(Controller.BasicAttackStatus.AcquireStatus(ActionStatusType.Disabled));
            presentationLocks.Add(Actor.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
        }
        else if (shardLocked) presentationLocks.Add(Controller.AcquireMoveStatus(1, ActionStatusType.Disabled));
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
        if (flying != appliedFlight)
        {
            if (flying) { previousFlight = Actor.PlayerMovement.IsFlying; Actor.PlayerMovement.IsFlying = true; }
            else Actor.PlayerMovement.IsFlying = previousFlight;
            appliedFlight = flying;
        }
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifetime.Dispose();
        movementLocked = flying = false;
        PresentMovement();
        foreach (var lease in presentationLocks) lease.Dispose();
        presentationLocks.Clear();
        projectiles.Clear();
        Swift = Shard = Wind = null;
    }
}

internal sealed class GlaceonCleanup : IDisposable
{
    private Action cleanup;
    public GlaceonCleanup(Action cleanup) { this.cleanup = cleanup; }
    public void Dispose() { var action = cleanup; cleanup = null; action?.Invoke(); }
}
