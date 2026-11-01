using System;
using System.Collections;
using System.Collections.Generic;
using Unite.Combat;
using UnityEngine;

public sealed class JolteonExecution : CombatExecution
{
    private readonly JolteonCharacterRuntime character;
    private readonly JoltRecastState recast = new JoltRecastState();
    private readonly HashSet<ulong> directHits = new HashSet<ulong>();
    private readonly HashSet<ulong> slowedTargets = new HashSet<ulong>();
    private readonly Dictionary<ulong, float> uniteHits = new Dictionary<ulong, float>();
    private readonly WallCrossingTraversal traversal = new WallCrossingTraversal();
    private CombatRoutine routine;
    private IDisposable actionLock, movementLock, castEffect;
    private Vector3 direction, nextDirection, steering;
    private Vector3 authoritativePosition;
    private int traversalSequence;
    private bool positionHeld;
    private bool stopped, recastRequested, traversalActive, dashActive, uniteActive;
    private bool passHit;

    public JolteonExecution(CombatExecutionContext context, JolteonCharacterRuntime character) : base(context)
    {
        this.character = character;
        Vector3 flat = new Vector3(context.Direction.x, 0f, context.Direction.z);
        direction = flat.sqrMagnitude > 0.001f ? flat.normalized : context.Actor.transform.forward;
    }

    public override bool FollowUpReady => !stopped && !recastRequested && recast.HasWindow && !recast.Expired(Time.time);

    private bool Alive => !stopped && Actor != null && Actor.IsSpawned && Actor.Pokemon != null && Actor.Pokemon.CurrentHp > 0;
    private bool Interrupted => !Alive || Actor.Pokemon.HasAnyStatusEffect(JoltMoveRuntime.MovementInterrupts);

    public override IEnumerator Prepare()
    {
        Prepared = false;
        if (!Actor.IsServer || character == null) yield break;
        bool loaded = false;
        yield return character.PrepareExecutionAssets(Context, success => loaded = success);
        Prepared = loaded && !Context.Scope.IsDisposed;
    }

    public override void Start()
    {
        if (!Prepared || stopped || !Actor.IsServer || Context.Scope.IsDisposed) return;
        routine = Context.Scope.Own(new CombatRoutine(Actor, Execute()));
    }

    public override bool TryRecast(Vector3 input, ulong targetId)
    {
        if (!CombatTargeting.Finite(input) || stopped) return false;
        input.y = 0f;
        if (uniteActive)
        {
            steering = input.sqrMagnitude > 0.05f ? input.normalized : Vector3.zero;
            return true;
        }
        if (Interrupted || !FollowUpReady || input.sqrMagnitude < 0.001f || !recast.TryRecast(Time.time)) return false;
        nextDirection = input.normalized;
        recastRequested = true;
        character.SetRecastWindow(Context.MoveId, false, 0d, recast.Empowered);
        Controller.Combat.PublishSlot(Context.Slot);
        return true;
    }

    internal void SetSteering(Vector2 input)
    {
        if (uniteActive) steering = new Vector3(input.x, 0f, input.y);
    }

    private IEnumerator Execute()
    {
        switch (Context.MoveId)
        {
            case AvailableMoves.JoltSwift: yield return ExecuteSwift(); break;
            case AvailableMoves.JoltAgility: yield return ExecuteAgility(); break;
            case AvailableMoves.JoltCharge: yield return ExecuteCharge(); break;
            case AvailableMoves.JoltThunderFang: yield return ExecuteThunderFang(); break;
            case AvailableMoves.JoltDischarge: yield return ExecuteDischarge(); break;
            case AvailableMoves.JoltElectroWeb: yield return ExecuteElectroweb(); break;
            case AvailableMoves.JolteonUnite: yield return ExecuteUnite(); break;
        }
    }

    private IEnumerator ExecuteSwift()
    {
        Face(direction);
        if (!character.SpawnSwiftProjectile(direction))
        {
            Finish(CastOutcome.ExecutionFailed);
            yield break;
        }
        Actor.AnimationManager?.PlayAnimation("ani_leafeonspell1_bat_0133_1");
        movementLock = character.AcquireMovementLock();
        yield return WaitForDuration(0.3f, true);
        ReleaseLocks();
        Finish(Interrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private IEnumerator ExecuteAgility()
    {
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.HindranceResistance, 0.5f, true, 0));
        Actor.Pokemon.AddStatChange(new StatChange(35, Stat.Speed, 3f, true, true, true, 0));
        character.ReplacePersistentEffect(JoltEffect.Agility, direction, false, Context.Upgraded);
        Finish(CastOutcome.Completed);
        yield break;
    }

    private IEnumerator ExecuteCharge()
    {
        bool empowered = character.PassiveReady;
        float duration = empowered ? 6f : 5f;
        Actor.Pokemon.RemoveAllStatChangeWithIDRPC(JoltMoveRules.ChargeBuff);
        Actor.Pokemon.AddStatChange(new StatChange((short)(empowered ? 35 : 25), Stat.Attack, duration, true, true, true, JoltMoveRules.ChargeBuff));
        short defenses = (short)((empowered ? 40 : 30) + (Context.Upgraded ? 10 : 0));
        Actor.Pokemon.AddStatChange(new StatChange(defenses, Stat.Defense, duration, true, true, true, JoltMoveRules.ChargeBuff));
        Actor.Pokemon.AddStatChange(new StatChange(defenses, Stat.SpDefense, duration, true, true, true, JoltMoveRules.ChargeBuff));
        Actor.Pokemon.AddStatChange(new StatChange((short)(empowered ? 35 : 30), Stat.Speed, empowered ? 5f : 2.5f, true, true, true, JoltMoveRules.ChargeBuff));
        JoltMoveRuntime.RefreshStat(Actor.Pokemon, new StatChange((short)(JoltMoveRules.ChargeSlow + (empowered ? 35 : 30)), Stat.Speed,
            JoltMoveRules.ChargeSlowDuration, true, false, true, JoltMoveRules.ChargeSlowId));
        character.ReplacePersistentEffect(JoltEffect.Charge, direction, empowered, Context.Upgraded);
        Actor.AnimationManager?.PlayAnimation("pm0135_00_ba21_tokusyu02");
        Finish(CastOutcome.Completed);
        yield break;
    }

    private IEnumerator ExecuteThunderFang()
    {
        recast.Begin(character.ConsumePassiveCharge());
        yield return ExecuteDashPass(true);
        if (Interrupted) { Finish(CastOutcome.Interrupted); yield break; }
        if (recast.Empowered)
        {
            yield return AwaitRecast(JoltMoveRules.FangWindow);
            if (!recastRequested) { Finish(Alive ? CastOutcome.Completed : CastOutcome.Interrupted); yield break; }
            if (recastRequested && !Interrupted)
            {
                direction = nextDirection;
                recastRequested = false;
                yield return ExecuteDashPass(true);
            }
        }
        Finish(Interrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private IEnumerator ExecuteDischarge()
    {
        recast.Begin(character.ConsumePassiveCharge());
        yield return ExecuteDashPass(false);
        if (Interrupted) { Finish(CastOutcome.Interrupted); yield break; }
        if (passHit)
        {
            yield return AwaitRecast(JoltMoveRules.DischargeWindow);
            if (!recastRequested) { Finish(Alive ? CastOutcome.Completed : CastOutcome.Interrupted); yield break; }
            if (recastRequested && !Interrupted)
            {
                direction = nextDirection;
                recastRequested = false;
                yield return ExecuteDashPass(false);
            }
        }
        Finish(Interrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private IEnumerator ExecuteElectroweb()
    {
        bool empowered = character.ConsumePassiveCharge();
        Face(direction);
        if (!character.SpawnElectroweb(direction, empowered, Context.Upgraded))
        {
            Finish(CastOutcome.ExecutionFailed);
            yield break;
        }
        movementLock = character.AcquireMovementLock();
        yield return WaitForDuration(0.15f, true);
        ReleaseLocks();
        Finish(Interrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private IEnumerator ExecuteUnite()
    {
        AcquireLocks(true);
        uniteActive = true;
        character.SetDashActive(true);
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, JoltMoveRules.BlitzDuration, true, JoltMoveRules.BlitzUnstoppable));
        Actor.AnimationManager?.SetBool("Walking", true);
        castEffect = character.SpawnCastEffect(JoltEffect.LightningBlitz, direction, false, Context.Upgraded, false);
        if (castEffect == null)
        {
            Finish(CastOutcome.ExecutionFailed);
            yield break;
        }
        character.BeginUnite(this, Context.CastId);
        Context.MarkCastSucceeded();
        BeginPositionHold();
        float remaining = JoltMoveRules.BlitzDuration;
        while (Alive && remaining > 0f)
        {
            float delta = Mathf.Min(Time.deltaTime, remaining);
            if (steering.sqrMagnitude > 0.05f)
                direction = Vector3.RotateTowards(direction, steering, 2.5f * delta, 0f).normalized;
            Actor.transform.position = authoritativePosition;
            AdvanceUnite(JoltMoveRules.BlitzSpeed * delta);
            Face(direction);
            SendPosition(true);
            remaining -= delta;
            yield return null;
        }
        if (Alive && remaining <= 0f)
        {
            JoltMoveRuntime.RefreshStat(Actor.Pokemon, new StatChange(80, Stat.Speed, 6f, true, true, true, JoltMoveRules.OverchargeSpeed));
            Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.2f), 135, 0, 6f, true));
            character.BeginUniteOvercharge(5f);
        }
        Finish(Alive ? CastOutcome.Completed : CastOutcome.Interrupted);
    }

    private IEnumerator ExecuteDashPass(bool fang)
    {
        passHit = false;
        directHits.Clear();
        slowedTargets.Clear();
        float range = fang ? JoltMoveRules.FangRange : recast.Empowered ? JoltMoveRules.BoostedDischargeRange : JoltMoveRules.DischargeRange;
        if (!fang && recast.IsSecondPass) range *= JoltMoveRules.RecastScale;
        float remaining = fang ? Vector3.Distance(Actor.transform.position,
            Actor.PlayerMovement.GetWallCrossingDestination(Actor.transform.position + direction * range)) : range;
        float elapsed = 0f, blocked = 0f, tick = 0f;
        Face(direction);
        AcquireLocks(true);
        dashActive = true;
        character.SetDashActive(true);
        BeginPositionHold();
        if (fang)
        {
            traversal.Begin(Actor.PlayerMovement);
            traversalActive = true;
            Actor.AnimationManager?.PlayAnimation("pm0135_00_ba20_thunderFang");
        }
        else Actor.AnimationManager?.SetBool("Walking", true);
        castEffect = character.SpawnCastEffect(fang ? JoltEffect.ThunderFang : JoltEffect.Discharge,
            direction, recast.Empowered, Context.Upgraded, recast.IsSecondPass);
        if (fang) yield return WaitForDuration(JoltMoveRules.FangStartup, true);
        while (!Interrupted && remaining > 0.02f && blocked < 0.06f && elapsed < (fang ? JoltMoveRules.FangDuration : 2f))
        {
            float delta = Time.deltaTime;
            if (delta <= 0f) { yield return null; continue; }
            float speed = fang ? range / JoltMoveRules.FangDuration : Actor.Pokemon.GetSpeed() / 1000f * (recast.IsSecondPass ? 3.5f : 2.5f);
            float requested = Mathf.Min(remaining, speed * delta);
            Actor.transform.position = authoritativePosition;
            Vector3 from = Actor.transform.position;
            Vector3 moved = fang ? traversal.Move(direction * requested) : Actor.PlayerMovement.MoveWithWalls(direction * requested);
            SendPosition(true);
            float actual = new Vector2(moved.x, moved.z).magnitude;
            remaining -= actual;
            elapsed += delta;
            blocked = actual < requested * 0.1f ? blocked + delta : 0f;
            ApplyDashHits(fang, from);
            if (!fang)
            {
                float radius = JoltMoveRules.DischargeRadius * (recast.IsSecondPass ? JoltMoveRules.RecastScale : 1f);
                if (recast.IsSecondPass)
                    foreach (Pokemon target in JoltMoveRuntime.Sweep(Actor, from, Actor.transform.position, radius)) Slow(target);
                tick -= delta;
                if (tick <= 0f)
                {
                    tick += JoltMoveRules.DischargeTick;
                    foreach (Pokemon target in JoltMoveRuntime.Sweep(Actor, Actor.transform.position, Actor.transform.position, radius))
                    {
                        target.TakeDamageRPC(new DamageInfo(Actor.NetworkObjectId, 1.3f, 3, 150, DamageType.Physical, JoltMoveRules.PeriodicDamage));
                        if (recast.IsSecondPass) Slow(target);
                    }
                }
            }
            yield return null;
        }
        EndPass();
        if (fang && passHit && !Interrupted)
            JoltMoveRuntime.RefreshStat(Actor.Pokemon, new StatChange(60, Stat.Speed, Context.Upgraded ? 2f : 1.5f, true, true, true, JoltMoveRules.FangSpeed));
    }

    private void ApplyDashHits(bool fang, Vector3 from)
    {
        Vector3 offset = fang ? direction * 0.8f : Vector3.zero;
        foreach (Pokemon target in JoltMoveRuntime.Sweep(Actor, from + offset, Actor.transform.position + offset, fang ? 1f : 0.75f))
        {
            if (!directHits.Add(target.NetworkObjectId)) continue;
            passHit = true;
            DamageInfo damage = fang
                ? new DamageInfo(Actor.NetworkObjectId, recast.Empowered ? 1.9f : 1.72f, (short)(recast.Empowered ? 10 : 9), (short)(recast.Empowered ? 400 : 350), DamageType.Physical, JoltMoveRules.DirectMoveDamage)
                : new DamageInfo(Actor.NetworkObjectId, 1.6f, 6, 300, DamageType.Physical, JoltMoveRules.DirectMoveDamage);
            target.TakeDamageRPC(damage);
            target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, fang ? 0.5f : 0.35f, true, 0));
            if (!fang && recast.IsSecondPass) Slow(target);
        }
    }

    private void Slow(Pokemon target)
    {
        if (slowedTargets.Add(target.NetworkObjectId))
            JoltMoveRuntime.RefreshStat(target, new StatChange((short)(Context.Upgraded ? 35 : 20), Stat.Speed, 1f, true, false, true, JoltMoveRules.DischargeSlow));
    }

    private IEnumerator AwaitRecast(float duration)
    {
        recast.Open(Time.time, duration);
        character.SetRecastWindow(Context.MoveId, true, Controller.Combat.Now + duration, recast.Empowered);
        Controller.Combat.PublishSlot(Context.Slot);
        while (Alive && !recastRequested && !recast.Expired(Time.time)) yield return null;
        character.SetRecastWindow(Context.MoveId, false, 0d, recast.Empowered);
        Controller.Combat.PublishSlot(Context.Slot);
    }

    private void AdvanceUnite(float distance)
    {
        Vector3 origin = Actor.transform.position;
        RaycastHit nearest = default;
        bool wall = false;
        foreach (RaycastHit hit in Physics.SphereCastAll(origin + Vector3.up, 0.65f, direction, distance + 0.05f))
        {
            if (!hit.collider.CompareTag("Wall") || (wall && hit.distance >= nearest.distance)) continue;
            nearest = hit;
            wall = true;
        }
        if (wall)
        {
            float toWall = Mathf.Max(0f, nearest.distance - 0.03f);
            MoveUniteAndHit(direction * Mathf.Min(toWall, distance));
            direction = JoltMoveRules.Reflect(direction, nearest.normal);
            MoveUniteAndHit(direction * Mathf.Max(0f, distance - toWall));
        }
        else
        {
            Vector3 moved = MoveUniteAndHit(direction * distance);
            if (distance > 0f && moved.sqrMagnitude < distance * distance * 0.01f) direction = -direction;
        }
    }

    private Vector3 MoveUniteAndHit(Vector3 displacement)
    {
        Vector3 origin = Actor.transform.position;
        Vector3 moved = Actor.PlayerMovement.MoveWithWalls(displacement);
        foreach (Pokemon target in JoltMoveRuntime.Sweep(Actor, origin, Actor.transform.position, 1f))
        {
            if (uniteHits.TryGetValue(target.NetworkObjectId, out float until) && Time.time < until) continue;
            uniteHits[target.NetworkObjectId] = Time.time + 0.7f;
            target.TakeDamageRPC(new DamageInfo(Actor.NetworkObjectId, 1.8f, 6, 300, DamageType.Physical, DamageProprieties.IsUniteMove));
            JoltMoveRuntime.RefreshStat(target, new StatChange(20, Stat.Speed, 2f, true, false, true, JoltMoveRules.BlitzSlow));
        }
        return moved;
    }

    private IEnumerator WaitForDuration(float duration, bool interruptible)
    {
        float until = Time.time + duration;
        while ((interruptible ? !Interrupted : Alive) && Time.time < until) yield return null;
    }

    private void AcquireLocks(bool movement)
    {
        actionLock = new CombatActionLock(Actor, false);
        if (movement) movementLock = character.AcquireMovementLock();
    }

    private void ReleaseLocks()
    {
        movementLock?.Dispose();
        movementLock = null;
        actionLock?.Dispose();
        actionLock = null;
    }

    private void EndPass()
    {
        if (positionHeld && Actor != null && Actor.IsSpawned) Actor.transform.position = authoritativePosition;
        if (traversalActive)
        {
            if (Alive) traversal.End(); else traversal.Cancel();
            traversalActive = false;
        }
        EndPositionHold();
        castEffect?.Dispose();
        castEffect = null;
        if (dashActive) character.SetDashActive(false);
        dashActive = false;
        ReleaseLocks();
    }

    private void BeginPositionHold()
    {
        authoritativePosition = Actor.transform.position;
        positionHeld = true;
        SendPosition(true);
    }

    private void SendPosition(bool active)
    {
        if (Actor == null || !Actor.IsSpawned) return;
        authoritativePosition = Actor.transform.position;
        character.SetTraversalPosition(Context.CastId, ++traversalSequence, authoritativePosition, direction, active);
    }

    private void EndPositionHold()
    {
        if (!positionHeld) return;
        positionHeld = false;
        SendPosition(false);
    }

    private void Face(Vector3 value)
    {
        value.y = 0f;
        if (value.sqrMagnitude > 0.001f) Actor.transform.rotation = Quaternion.LookRotation(value.normalized);
    }

    private void Finish(CastOutcome outcome)
    {
        if (!stopped) Context.Finish(outcome);
    }

    public override void Stop(CastOutcome reason)
    {
        if (stopped) return;
        stopped = true;
        routine?.Dispose();
        routine = null;
        EndPass();
        if (uniteActive)
        {
            uniteActive = false;
            character.EndUnite(this);
            character.SetDashActive(false);
            if (Actor != null && Actor.Pokemon != null && Actor.IsSpawned)
                Actor.Pokemon.RemoveStatusEffectWithID(JoltMoveRules.BlitzUnstoppable);
        }
        character.SetRecastWindow(Context.MoveId, false, 0d, false);
        recast.Clear();
        uniteHits.Clear();
    }
}
