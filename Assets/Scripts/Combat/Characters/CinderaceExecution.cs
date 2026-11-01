using System;
using System.Collections;
using Unite.Combat;
using UnityEngine;

public sealed class CinderaceExecution : CombatExecution
{
    private readonly CinderaceCharacterRuntime character;
    private readonly CinderCast kind;
    private Vector3 direction;
    private GameObject prefab;
    private CinderMoveObject effect;
    private CombatRoutine routine;
    private IDisposable movementLease;
    private string animation;
    private bool stopped;

    public CinderaceExecution(CombatExecutionContext context, CinderaceCharacterRuntime character) : base(context)
    {
        this.character = character;
        kind = CinderaceCharacterRuntime.KindForMove(context.MoveId);
        direction = CinderMoveRules.Direction(context.Direction, context.Actor.transform.forward);
    }

    public override IEnumerator Prepare()
    {
        Prepared = false;
        if (!Actor.IsServer) yield break;
        yield return Controller.Combat.Spawner.Load(CinderMoveRules.Path(kind), Context.Scope, loaded => prefab = loaded);
        if (prefab == null || Context.Scope.IsDisposed) yield break;
        Prepared = prefab.GetComponent<CinderMoveObject>() != null;
    }

    public override void Start()
    {
        if (!Prepared || stopped || !Actor.IsServer || Context.Scope.IsDisposed) return;
        routine = Context.Scope.Own(new CombatRoutine(Actor, Execute()));
    }

    private bool Alive => !stopped && Actor != null && Actor.IsSpawned && Actor.Pokemon != null && Actor.Pokemon.CurrentHp > 0;

    private bool Interrupted
    {
        get
        {
            if (!Alive) return true;
            if (kind == CinderCast.BlazeKick || kind == CinderCast.Unite) return false;
            if (Actor.Pokemon.HasAnyStatusEffect(CinderMoveRules.Interrupts)) return true;
            return CinderMoveRules.Dash(kind) && Actor.Pokemon.HasStatusEffect(StatusType.Immobilized);
        }
    }

    private IEnumerator Execute()
    {
        switch (Context.MoveId)
        {
            case AvailableMoves.CinderEmber: yield return ExecuteEmber(); break;
            case AvailableMoves.CinderLowSweep: yield return ExecuteLowSweep(); break;
            case AvailableMoves.CinderPyroball: yield return ExecutePyroBall(); break;
            case AvailableMoves.CinderBlazeKick: yield return ExecuteBlazeKick(); break;
            case AvailableMoves.CinderFlameCharge: yield return ExecuteFlameCharge(); break;
            case AvailableMoves.CinderFeint: yield return ExecuteFeint(); break;
            case AvailableMoves.BlazingBycicleKick: yield return ExecuteUnite(); break;
        }
    }

    private IEnumerator ExecuteEmber()
    {
        if (!BeginCast(CinderCast.Ember, "ani_spell1_bat_0815", true)) yield break;
        yield return WaitForRecovery(CinderMoveRules.Recovery(CinderCast.Ember));
        FinishCast();
    }

    private IEnumerator ExecuteLowSweep()
    {
        if (!BeginCast(CinderCast.LowSweep, "ani_spell2_bat_0815", true)) yield break;
        yield return ExecuteDash();
        FinishCast();
    }

    private IEnumerator ExecutePyroBall()
    {
        if (!BeginCast(CinderCast.PyroBall, "ani_spell1a_bat_0815", true)) yield break;
        yield return WaitForRecovery(CinderMoveRules.Recovery(CinderCast.PyroBall));
        FinishCast();
    }

    private IEnumerator ExecuteBlazeKick()
    {
        if (!BeginCast(CinderCast.BlazeKick, "ani_spell1b1_bat_0815", true)) yield break;
        yield return ExecuteKickApproachAndRebound();
        FinishCast();
    }

    private IEnumerator ExecuteFlameCharge()
    {
        if (!BeginCast(CinderCast.FlameCharge, "ani_spell2a_bat_0815", true)) yield break;
        yield return ExecuteDash();
        FinishCast();
    }

    private IEnumerator ExecuteFeint()
    {
        if (!BeginCast(CinderCast.Feint, "ani_spell2b_bat_0815", false)) yield break;
        yield return WaitForRecovery(CinderMoveRules.Recovery(CinderCast.Feint));
        FinishCast();
    }

    private IEnumerator ExecuteUnite()
    {
        if (!BeginCast(CinderCast.Unite, "ani_spellu_bat_0815", true)) yield break;
        yield return WaitForRecovery(CinderMoveRules.Recovery(CinderCast.Unite));
        FinishCast();
    }

    private bool BeginCast(CinderCast move, string moveAnimation, bool lockMovement)
    {
        Context.Scope.Own(new CombatActionLock(Actor, lockMovement));
        if (lockMovement) movementLease = Context.Scope.Own(character.AcquireMovementLock());
        int castId = unchecked((int)Context.CastId);
        if (castId == 0) castId = 1;
        bool castOwnsEffect = CinderMoveRules.Dash(move) || move == CinderCast.BlazeKick || move == CinderCast.Feint;
        effect = character.SpawnPreparedObject(prefab, move, castId, direction, Context.Target, Context.Upgraded, false, false, false, Context.Scope, castOwnsEffect);
        if (effect == null)
        {
            movementLease?.Dispose();
            movementLease = null;
            Context.Finish(CastOutcome.ExecutionFailed);
            return false;
        }
        Face(direction);
        animation = moveAnimation;
        Animate(animation);
        return true;
    }

    private IEnumerator WaitForRecovery(float recovery)
    {
        float started = Time.time;
        while (!Interrupted && Time.time - started < recovery) yield return null;
    }

    private IEnumerator ExecuteDash()
    {
        Vector3 origin = Actor.transform.position;
        Vector3 destination = origin + direction * CinderMoveRules.DashDistance;
        character.BeginDash(origin, destination, CinderMoveRules.DashDuration);
        Context.Scope.Own(new CombatCallbackLease(() => character.EndDash(false)));
        yield return WaitForRecovery(CinderMoveRules.DashDuration);
        bool completed = !Interrupted;
        character.EndDash(completed);
        if (completed && effect != null) effect.MovementPhase(true, Actor.transform.position);
    }

    private IEnumerator ExecuteKickApproachAndRebound()
    {
        Vector3 approach = Context.Target != null
            ? CinderMoveRules.Direction(Context.Target.transform.position - Actor.transform.position, direction)
            : direction;
        float distance = Context.Target != null
            ? Mathf.Max(0f, Vector3.Distance(Context.Target.transform.position, Actor.transform.position) - 0.8f)
            : 0f;
        Vector3 origin = Actor.transform.position;
        character.BeginDash(origin, origin + approach * distance, CinderMoveRules.KickApproach);
        Context.Scope.Own(new CombatCallbackLease(() => character.EndDash(false)));
        yield return WaitForRecovery(CinderMoveRules.KickApproach);
        if (Interrupted)
        {
            character.EndDash(false);
            yield break;
        }
        character.EndDash(true);
        if (effect != null) effect.MovementPhase(false, Actor.transform.position);

        animation = "ani_spell1b2_bat_0815";
        Animate(animation);
        Vector3 rebound = character.ReboundDirection(-approach);
        Vector3 reboundOrigin = Actor.transform.position;
        character.BeginDash(reboundOrigin, reboundOrigin + rebound * CinderMoveRules.ReboundDistance, CinderMoveRules.KickRebound);
        yield return WaitForRecovery(CinderMoveRules.KickRebound);
        bool completed = !Interrupted;
        character.EndDash(completed);
        if (completed && effect != null) effect.MovementPhase(true, Actor.transform.position);
    }

    private void FinishCast()
    {
        movementLease?.Dispose();
        movementLease = null;
        if (!Interrupted) character.RecoverAnimation(animation);
        if (!stopped) Context.Finish(Interrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private void Animate(string moveAnimation) => Actor.AnimationManager?.PlayAnimation(moveAnimation);

    private void Face(Vector3 value)
    {
        value.y = 0f;
        if (value.sqrMagnitude > 0.001f) Actor.transform.rotation = Quaternion.LookRotation(value);
    }

    public override void Stop(CastOutcome reason)
    {
        if (stopped) return;
        stopped = true;
        routine?.Dispose();
        routine = null;
        movementLease?.Dispose();
        movementLease = null;
        if (reason != CastOutcome.Completed && effect != null && effect.IsSpawned) effect.Despawn();
    }
}
