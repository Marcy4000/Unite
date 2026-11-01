using System;
using System.Collections;
using Unite.Combat;
using UnityEngine;

public sealed class VaporeonExecution : CombatExecution
{
    private readonly VaporeonCharacterRuntime character;
    private CombatRoutine routine;
    private WallCrossingTraversal traversal;
    private bool stopped, diveReady, emergeRequested;
    private double diveStarted;
    private bool hasDiveExit;
    private Vector3 requestedDiveExit;

    public VaporeonExecution(CombatExecutionContext context, VaporeonCharacterRuntime character) : base(context)
    {
        this.character = character;
    }

    private bool Alive => !stopped && Actor != null && Actor.IsSpawned && Actor.Pokemon != null && Actor.Pokemon.CurrentHp > 0;
    private bool Interrupted => !Alive || Actor.Pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts);
    public override bool FollowUpReady => diveReady && !emergeRequested && Alive;

    public override void Start()
    {
        if (!Actor.IsServer || stopped || Context.Scope.IsDisposed) return;
        routine = Context.Scope.Own(new CombatRoutine(Actor, Execute()));
    }

    private IEnumerator Execute()
    {
        switch (Context.MoveId)
        {
            case AvailableMoves.VaporSwift:
                yield return ExecuteSwift();
                break;
            case AvailableMoves.VaporHelpingHand:
                yield return ExecuteHelpingHand();
                break;
            case AvailableMoves.VaporAquaRing:
                yield return ExecuteAquaRing();
                break;
            case AvailableMoves.VaporRainDance:
                yield return ExecuteRainDance();
                break;
            case AvailableMoves.VaporWhirlpool:
                yield return ExecuteWhirlpool();
                break;
            case AvailableMoves.VaporDive:
                yield return ExecuteDive();
                break;
            case AvailableMoves.VaporeonUnite:
                yield return ExecuteUnite();
                break;
        }
    }

    private IEnumerator ExecuteSwift()
    {
        Context.Scope.Own(character.TrackRecovery(VaporMoveRules.Windup(VaporCast.Swift) + VaporMoveRules.Recovery(VaporCast.Swift)));
        Context.Scope.Own(character.AcquireActionLock(true));
        Vector3 center = Destination(VaporCast.Swift);
        Face(center - Actor.transform.position);
        Animate("Armature_pm0134_00_ba21_tokusyu01_gfbanm");
        yield return WaitWhileCasting(VaporMoveRules.Windup(VaporCast.Swift));
        if (Interrupted) { FinishInterrupted(); yield break; }
        character.StartSwiftVolley(center);
        yield return WaitWhileCasting(VaporMoveRules.Recovery(VaporCast.Swift));
        FinishCast();
    }

    private IEnumerator ExecuteHelpingHand()
    {
        if (!VaporMoveRuntime.CanHeal(Actor, Context.Target) || Context.Target == Actor.Pokemon)
        {
            Context.Finish(CastOutcome.ExecutionFailed);
            yield break;
        }
        Context.Scope.Own(character.TrackRecovery(VaporMoveRules.Recovery(VaporCast.HelpingHand)));
        Context.Scope.Own(character.AcquireActionLock(false));
        Face(Context.Target.transform.position - Actor.transform.position);
        Animate("Armature_pm0134_00_ba21_tokusyu01_gfbanm");
        character.StartHelpingHand(Context.Target);
        yield return WaitWhileCasting(VaporMoveRules.Recovery(VaporCast.HelpingHand));
        FinishCast();
    }

    private IEnumerator ExecuteAquaRing()
    {
        Pokemon recipient = Context.Target != null ? Context.Target : Actor.Pokemon;
        if (!VaporMoveRuntime.CanHeal(Actor, recipient))
        {
            Context.Finish(CastOutcome.ExecutionFailed);
            yield break;
        }
        Context.Scope.Own(character.TrackRecovery(VaporMoveRules.Recovery(VaporCast.AquaRing)));
        Context.Scope.Own(character.AcquireActionLock(false));
        Face(recipient.transform.position - Actor.transform.position);
        Animate("Armature_pm0134_00_ba21_tokusyu01_gfbanm");
        character.StartAquaRing(recipient, Context.Upgraded);
        yield return WaitWhileCasting(VaporMoveRules.Recovery(VaporCast.AquaRing));
        FinishCast();
    }

    private IEnumerator ExecuteRainDance()
    {
        Context.Scope.Own(character.TrackRecovery(VaporMoveRules.Windup(VaporCast.RainDance) + VaporMoveRules.Recovery(VaporCast.RainDance)));
        Context.Scope.Own(character.AcquireActionLock(true));
        character.CancelRainDance();
        Vector3 origin = Actor.transform.position;
        Vector3 destination = Actor.PlayerMovement.VaporeonHopDestination(Destination(VaporCast.RainDance));
        character.StartHop(origin, destination);
        Context.Scope.Own(new CombatCallbackLease(character.EndHop));
        Face(destination - origin);
        Animate("Armature_pm0134_00_kw35_playA01_gfbanm");
        traversal = new WallCrossingTraversal();
        traversal.Begin(Actor.PlayerMovement);
        Context.Scope.Own(new CombatCallbackLease(CancelTraversal));
        double started = character.Now;
        float windup = VaporMoveRules.Windup(VaporCast.RainDance);
        while (!Interrupted && character.Now - started < windup)
        {
            Vector3 next = Vector3.Lerp(origin, destination, Mathf.Clamp01((float)(character.Now - started) / windup));
            traversal.Move(next - Actor.transform.position);
            yield return null;
        }
        if (Interrupted) { FinishInterrupted(); yield break; }
        traversal.Move(destination - Actor.transform.position);
        traversal.End();
        traversal = null;
        character.EndHop();
        character.StartRainDance(Actor.transform.position, Context.Upgraded);
        yield return WaitWhileCasting(VaporMoveRules.Recovery(VaporCast.RainDance));
        FinishCast();
    }

    private IEnumerator ExecuteWhirlpool()
    {
        Context.Scope.Own(character.TrackRecovery(VaporMoveRules.Windup(VaporCast.Whirlpool) + VaporMoveRules.Recovery(VaporCast.Whirlpool)));
        Context.Scope.Own(character.AcquireActionLock(true));
        Vector3 center = Destination(VaporCast.Whirlpool);
        Face(center - Actor.transform.position);
        Animate("Armature_pm0134_00_ba21_tokusyu01_gfbanm");
        yield return WaitWhileCasting(VaporMoveRules.Windup(VaporCast.Whirlpool));
        if (Interrupted) { FinishInterrupted(); yield break; }
        character.StartWhirlpool(center, Context.Upgraded);
        yield return WaitWhileCasting(VaporMoveRules.Recovery(VaporCast.Whirlpool));
        FinishCast();
    }

    private IEnumerator ExecuteDive()
    {
        Context.Scope.Own(character.AcquireActionLock(false, lockServer: false, exceptSlot: 1));
        character.SetDiving(true);
        Context.Scope.Own(new CombatCallbackLease(() => character.SetDiving(false)));
        PlayerMovement movement = Actor.PlayerMovement;
        movement.BeginVaporeonDiveTraversal();
        Context.Scope.Own(new CombatCallbackLease(() => { if (movement != null) movement.EndVaporeonDiveTraversal(movement.IsOwner); }));
        ushort invincible = VaporEffectIds.Acquire();
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, VaporMoveRules.DiveDuration + 0.5f, true, invincible));
        Context.Scope.Own(new CombatCallbackLease(() =>
        {
            if (Actor != null && Actor.Pokemon != null && Actor.Pokemon.IsSpawned) Actor.Pokemon.RemoveStatusEffectWithID(invincible);
            VaporEffectIds.Release(invincible);
        }));
        int wake = character.ShowEffect(VaporEffect.DiveWake, Actor.transform.position,
            VaporMoveRules.DiveDuration + VaporMoveRules.DiveWarning, Controller.NetworkObjectId, VaporMoveRules.DiveRadius);
        Context.Scope.Own(new CombatCallbackLease(() => character.StopEffect(wake)));
        Animate("Armature_pm0134_00_ba01_landA01_gfbanm", 3f);
        diveStarted = character.Now;
        while (Alive && !emergeRequested && character.Now - diveStarted < VaporMoveRules.DiveDuration)
        {
            movement.TrackVaporeonDivePosition();
            if (!diveReady && character.Now - diveStarted >= VaporMoveRules.DiveMinimum)
            {
                diveReady = true;
                Controller.Combat.PublishSlot(Context.Slot);
            }
            yield return null;
        }
        diveReady = false;
        if (!Alive) { FinishInterrupted(); yield break; }
        Vector3 center = Actor.transform.position;
        if (movement.TryVaporeonDiveExit(hasDiveExit ? requestedDiveExit : center, out Vector3 safeExit)) center = safeExit;
        movement.EndVaporeonDiveTraversal(false);
        movement.VaporeonDiveExitRPC(center);
        Animate("Armature_pm0134_00_ba01_landC01_gfbanm", 3f);
        character.StartDiveImpact(center);
        if (!stopped) Context.Finish(CastOutcome.Completed);
    }

    private IEnumerator ExecuteUnite()
    {
        Context.Scope.Own(character.TrackRecovery(VaporMoveRules.Recovery(VaporCast.Tempest)));
        Context.Scope.Own(character.AcquireActionLock(true));
        Vector3 center = Actor.transform.position;
        Animate("Armature_pm0134_00_ba02_roar01_gfbanm");
        int warning = character.ShowEffect(VaporEffect.TempestWarning, center, VaporMoveRules.TempestWindup, size: VaporMoveRules.TempestRadius);
        Context.Scope.Own(new CombatCallbackLease(() => character.StopEffect(warning)));
        yield return WaitWhileCasting(VaporMoveRules.TempestWindup);
        if (Interrupted) { FinishInterrupted(); yield break; }
        character.ApplyTempest(center);
        Context.MarkCastSucceeded();
        yield return WaitWhileCasting(Mathf.Max(0f, VaporMoveRules.Recovery(VaporCast.Tempest) - VaporMoveRules.TempestWindup));
        FinishCast();
    }

    public override bool TryRecast(Vector3 direction, ulong targetId)
    {
        if (!FollowUpReady || character.Now - diveStarted < VaporMoveRules.DiveMinimum) return false;
        emergeRequested = true;
        diveReady = false;
        Vector3 offset = direction - Actor.transform.position;
        offset.y = 0;
        if (VaporMoveRules.Finite(direction) && offset.sqrMagnitude <= 9f && Actor.PlayerMovement.IsVaporeonDiveLandingValid(direction))
        {
            requestedDiveExit = direction;
            requestedDiveExit.y = Actor.transform.position.y;
            hasDiveExit = true;
        }
        return true;
    }

    private Vector3 Destination(VaporCast kind)
        => VaporMoveRules.ClampDestination(Actor.transform.position, Actor.transform.position + Context.Direction, VaporMoveRules.Range(kind));

    private void Face(Vector3 direction)
    {
        direction.y = 0;
        if (direction.sqrMagnitude > 0.001f) Actor.transform.rotation = Quaternion.LookRotation(direction);
    }

    private void Animate(string animation, float speed = 2.5f) => VaporMoveRuntime.Animate(Actor, animation, speed: speed);

    private IEnumerator WaitWhileCasting(float duration)
    {
        double end = character.Now + duration;
        while (!Interrupted && character.Now < end) yield return null;
    }

    private void FinishInterrupted() { if (!stopped) Context.Finish(CastOutcome.Interrupted); }
    private void FinishCast()
    {
        if (stopped) return;
        if (!Interrupted) character.RecoverAnimation();
        Context.Finish(Interrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private void CancelTraversal()
    {
        if (traversal == null) return;
        if (Actor != null && Actor.IsSpawned && Actor.Pokemon != null && Actor.Pokemon.CurrentHp > 0) traversal.End();
        else traversal.Cancel();
        traversal = null;
    }

    public override void Stop(CastOutcome reason)
    {
        if (stopped) return;
        stopped = true;
        diveReady = false;
        routine?.Dispose();
        routine = null;
        CancelTraversal();
    }
}
