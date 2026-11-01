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
        string path = CinderMoveRules.Path(kind);
        yield return Controller.Combat.Spawner.Load(path, Context.Scope, loaded => prefab = loaded);
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
            if (CinderMoveRules.Dash(kind) && Actor.Pokemon.HasStatusEffect(StatusType.Immobilized)) return true;
            return false;
        }
    }

    private IEnumerator Execute()
    {
        bool lockMovement = kind != CinderCast.Feint;
        Context.Scope.Own(new CombatActionLock(Actor, lockMovement));
        IDisposable movementLease = lockMovement ? Context.Scope.Own(character.AcquireMovementLock()) : null;
        int castId = unchecked((int)Context.CastId);
        if (castId == 0) castId = 1;
        bool despawnWithScope = CinderMoveRules.Dash(kind) || kind == CinderCast.BlazeKick || kind == CinderCast.Feint;
        effect = character.SpawnPreparedObject(prefab, kind, castId, direction, Context.Target, Context.Upgraded, false, false, false, Context.Scope, despawnWithScope);
        if (effect == null)
        {
            Context.Finish(CastOutcome.ExecutionFailed);
            yield break;
        }
        Face(direction);
        animation = AnimationForKind(kind);
        Animate(animation);

        if (CinderMoveRules.Dash(kind))
        {
            yield return ExecuteDash();
        }
        else if (kind == CinderCast.BlazeKick)
        {
            yield return ExecuteBlazeKick();
        }
        else
        {
            float recovery = CinderMoveRules.Recovery(kind);
            float started = Time.time;
            while (!Interrupted && Time.time - started < recovery)
                yield return null;
        }

        movementLease?.Dispose();
        if (!Interrupted)
            character.RecoverAnimation(animation);
        if (!stopped)
            Context.Finish(Interrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private IEnumerator ExecuteDash()
    {
        Vector3 origin = Actor.transform.position;
        Vector3 destination = origin + direction * CinderMoveRules.DashDistance;
        character.BeginDash(origin, destination, CinderMoveRules.DashDuration);
        Context.Scope.Own(new CombatCallbackLease(() => character.EndDash(false)));
        float started = Time.time;
        while (!Interrupted && Time.time - started < CinderMoveRules.DashDuration)
            yield return null;
        bool completed = !Interrupted;
        character.EndDash(completed);
        if (completed && effect != null)
            effect.MovementPhase(true, Actor.transform.position);
    }

    private IEnumerator ExecuteBlazeKick()
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
        float started = Time.time;
        while (!Interrupted && Time.time - started < CinderMoveRules.KickApproach)
            yield return null;
        if (Interrupted)
        {
            character.EndDash(false);
            yield break;
        }
        character.EndDash(true);
        if (effect != null)
            effect.MovementPhase(false, Actor.transform.position);

        animation = "ani_spell1b2_bat_0815";
        Animate(animation);
        Vector3 rebound = character.ReboundDirection(-approach);
        Vector3 reboundOrigin = Actor.transform.position;
        character.BeginDash(reboundOrigin, reboundOrigin + rebound * CinderMoveRules.ReboundDistance, CinderMoveRules.KickRebound);
        started = Time.time;
        while (!Interrupted && Time.time - started < CinderMoveRules.KickRebound)
            yield return null;
        bool completed = !Interrupted;
        character.EndDash(completed);
        if (completed && effect != null)
            effect.MovementPhase(true, Actor.transform.position);
    }

    private static string AnimationForKind(CinderCast kind)
    {
        switch (kind)
        {
            case CinderCast.Ember: return "ani_spell1_bat_0815";
            case CinderCast.PyroBall: return "ani_spell1a_bat_0815";
            case CinderCast.BlazeKick: return "ani_spell1b1_bat_0815";
            case CinderCast.LowSweep: return "ani_spell2_bat_0815";
            case CinderCast.FlameCharge: return "ani_spell2a_bat_0815";
            case CinderCast.Feint: return "ani_spell2b_bat_0815";
            default: return "ani_spellu_bat_0815";
        }
    }

    private void Animate(string animation)
    {
        Actor.AnimationManager?.PlayAnimation(animation);
    }

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
        if (reason != CastOutcome.Completed && effect != null && effect.IsSpawned)
            effect.Despawn();
    }
}
