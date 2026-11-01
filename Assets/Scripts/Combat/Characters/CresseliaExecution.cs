using System;
using System.Collections;
using Unite.Combat;
using UnityEngine;

public sealed class CresseliaExecution : CombatExecution
{
    private readonly CresseliaCharacterRuntime character;
    private readonly Vector3 direction;
    private CombatRoutine routine;
    private string animation;
    private bool followUpReady;
    private bool recastRequested;
    private bool uniteLanding;
    private bool uniteLanded;
    private bool stopped;

    public override bool FollowUpReady => followUpReady;

    public CresseliaExecution(CombatExecutionContext context, CresseliaCharacterRuntime character) : base(context)
    {
        this.character = character;
        direction = CresseliaMoveRules.Direction(context.Direction, context.Actor.transform.forward);
    }

    public override IEnumerator Prepare()
    {
        Prepared = Actor != null && Actor.IsServer && !Context.Scope.IsDisposed;
        yield break;
    }

    public override void Start()
    {
        if (!Prepared || stopped || !Actor.IsServer || Context.Scope.IsDisposed) return;
        routine = Context.Scope.Own(new CombatRoutine(Actor, Execute()));
    }

    public override bool TryRecast(Vector3 recastDirection, ulong targetId)
    {
        if (Context.MoveId != AvailableMoves.CresseliaUnite || stopped || !followUpReady || uniteLanding || !Alive) return false;
        recastRequested = true;
        followUpReady = false;
        Controller.Combat?.PublishSlot(Context.Slot);
        return true;
    }

    private bool Alive => !stopped && Actor != null && Actor.IsSpawned && Actor.Pokemon != null && Actor.Pokemon.CurrentHp > 0;
    private bool Interrupted => !Alive || (Context.MoveId != AvailableMoves.CresseliaUnite && Actor.Pokemon.HasAnyStatusEffect(CresseliaMoveRules.Interrupts));

    private IEnumerator Execute()
    {
        switch (Context.MoveId)
        {
            case AvailableMoves.CresseliaAuroraBeam: yield return ExecuteAuroraBeam(); break;
            case AvailableMoves.CresseliaPsybeam: yield return ExecutePsybeam(); break;
            case AvailableMoves.CresseliaMoonblast: yield return ExecuteMoonblast(); break;
            case AvailableMoves.CresseliaMoonlight: yield return ExecuteMoonlight(); break;
            case AvailableMoves.CresseliaSafeguard: yield return ExecuteSafeguard(); break;
            case AvailableMoves.CresseliaLunarBlessing: yield return ExecuteLunarBlessing(); break;
            case AvailableMoves.CresseliaUnite: yield return ExecuteUnite(); break;
        }
    }

    private IEnumerator ExecuteAuroraBeam()
    {
        BeginOrdinaryCast("pm0488_ba21_tokusyu01", 2.5f, true);
        yield return WaitForWindup(CresseliaCast.AuroraBeam);
        if (Interrupted) { FinishInterrupted(); yield break; }
        character.LaunchAuroraBeam(direction);
        yield return RecoverOrdinaryCast(CresseliaMoveRules.Recovery(CresseliaCast.AuroraBeam));
    }

    private IEnumerator ExecutePsybeam()
    {
        BeginOrdinaryCast("pm0488_ba21_tokusyu01", 2.5f, true);
        yield return WaitForWindup(CresseliaCast.Psybeam);
        if (Interrupted) { FinishInterrupted(); yield break; }
        character.ExecutePsybeam(direction, Context.Upgraded);
        yield return RecoverOrdinaryCast(CresseliaMoveRules.Recovery(CresseliaCast.Psybeam));
    }

    private IEnumerator ExecuteMoonblast()
    {
        BeginOrdinaryCast("pm0488_ba21_tokusyu01", 2.5f, true);
        yield return WaitForWindup(CresseliaCast.Moonblast);
        if (Interrupted) { FinishInterrupted(); yield break; }
        character.LaunchMoonblast(direction, Context.Upgraded);
        yield return RecoverOrdinaryCast(CresseliaMoveRules.Recovery(CresseliaCast.Moonblast));
    }

    private IEnumerator ExecuteMoonlight()
    {
        BeginOrdinaryCast("pm0488_ba21_tokusyu01", 2.5f, true);
        yield return WaitForWindup(CresseliaCast.Moonlight);
        if (Interrupted) { FinishInterrupted(); yield break; }
        Vector3 center = Context.CharacterData is Vector3 point ? point : Actor.transform.position;
        character.StartMoonlightField(center, Context.Upgraded);
        yield return RecoverOrdinaryCast(CresseliaMoveRules.Recovery(CresseliaCast.Moonlight));
    }

    private IEnumerator ExecuteSafeguard()
    {
        BeginOrdinaryCast("pm0488_ba21_tokusyu02", 3.5f, false);
        if (Interrupted) { FinishInterrupted(); yield break; }
        character.ExecuteSafeguard(Context.Target);
        yield return RecoverOrdinaryCast(CresseliaMoveRules.Recovery(CresseliaCast.Safeguard));
    }

    private IEnumerator ExecuteLunarBlessing()
    {
        BeginOrdinaryCast("pm0488_ba21_tokusyu02", 3.5f, false);
        if (Interrupted) { FinishInterrupted(); yield break; }
        character.ExecuteLunarBlessing(Context.Target, Context.Upgraded);
        yield return RecoverOrdinaryCast(CresseliaMoveRules.Recovery(CresseliaCast.LunarBlessing));
    }

    private void BeginOrdinaryCast(string state, float actionSpeed, bool lockMovement)
    {
        Context.Scope.Own(new CombatActionLock(Actor, lockMovement));
        if (lockMovement)
            Context.Scope.Own(character.AcquireMovementLock());
        Face(direction);
        animation = state;
        Actor.AnimationManager?.SetFloat("CresseliaActionSpeed", actionSpeed);
        Actor.AnimationManager?.PlayAnimation(animation);
    }

    private IEnumerator WaitForWindup(CresseliaCast move)
    {
        float windup = CresseliaMoveRules.Windup(move);
        float started = Time.time;
        while (!Interrupted && Time.time - started < windup)
            yield return null;
    }

    private IEnumerator RecoverOrdinaryCast(float duration)
    {
        float started = Time.time;
        while (!Interrupted && Time.time - started < duration)
            yield return null;
        if (!Interrupted)
            character.RecoverAnimation(animation);
        if (!stopped)
            Context.Finish(Interrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private void FinishInterrupted()
    {
        if (!stopped) Context.Finish(CastOutcome.Interrupted);
    }

    private IEnumerator ExecuteUnite()
    {
        Vector3 takeoff = Actor.transform.position;
        if (Actor.PlayerMovement != null && Actor.PlayerMovement.TryCresseliaLanding(takeoff, out Vector3 ground))
            takeoff.y = ground.y;
        Vector3 finalPoint = takeoff;

        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, 0f, false, CresseliaMoveRules.UniteInvincibleId));
        int flightVisual = character.ShowEffect(CresseliaEffect.UniteFlight, Actor.transform.position, Actor.transform.forward,
            CresseliaMoveRules.FlightDuration + CresseliaMoveRules.LandingDuration, Controller.NetworkObjectId);
        int warningVisual = character.ShowEffect(CresseliaEffect.UniteWarning, Actor.transform.position, Actor.transform.forward,
            CresseliaMoveRules.FlightDuration, Controller.NetworkObjectId, CresseliaMoveRules.UniteRadius, 1);

        IDisposable uniteLease = Context.Scope.Own(character.AcquireUniteLock());
        Context.Scope.Own(new CombatCallbackLease(() =>
        {
            followUpReady = false;
            if (Actor != null && Actor.Pokemon != null && Actor.Pokemon.IsSpawned)
                Actor.Pokemon.RemoveStatusEffectWithID(CresseliaMoveRules.UniteInvincibleId);
            character.StopEffect(flightVisual);
            character.StopEffect(warningVisual);
            uniteLease.Dispose();
            bool alive = Actor != null && Actor.Pokemon != null && Actor.Pokemon.CurrentHp > 0;
            if (alive && !uniteLanded && Actor.PlayerMovement != null
                && Actor.PlayerMovement.TryCresseliaLanding(Actor.transform.position, out Vector3 cancelLanding))
            {
                finalPoint = cancelLanding;
            }
            character.SetUnitePhase(alive ? 0 : -1, finalPoint, finalPoint, character.CurrentTime);
            if (alive && !string.IsNullOrEmpty(animation))
                character.RecoverAnimation(animation);
        }));

        IDisposable takeoffMovement = Context.Scope.Own(character.AcquireMovementLock());
        IDisposable takeoffLock = Context.Scope.Own(new CombatActionLock(Actor, true, lockServer: true, lockMoves: false));
        double flightStarted = character.CurrentTime;
        animation = "pm0488_ba02_roar01";
        Actor.AnimationManager?.SetFloat("CresseliaActionSpeed", 1.5f);
        Actor.AnimationManager?.PlayAnimation(animation);
        character.SetUnitePhase(1, takeoff, takeoff, flightStarted);

        while (Alive && character.CurrentTime < flightStarted + CresseliaMoveRules.TakeoffDuration)
            yield return null;

        takeoffMovement.Dispose();
        takeoffLock.Dispose();
        if (!Alive)
        {
            if (!stopped) Context.Finish(CastOutcome.Interrupted);
            yield break;
        }

        animation = "pm0488_ba10_waitB01";
        followUpReady = true;
        Controller.Combat?.PublishSlot(Context.Slot);

        while (Alive && !recastRequested && character.CurrentTime < flightStarted + CresseliaMoveRules.FlightDuration)
            yield return null;

        followUpReady = false;
        Controller.Combat?.PublishSlot(Context.Slot);
        if (!Alive)
        {
            if (!stopped) Context.Finish(CastOutcome.Interrupted);
            yield break;
        }

        if (Actor.PlayerMovement != null && !Actor.PlayerMovement.TryCresseliaLanding(Actor.transform.position, out finalPoint))
        {
            if (!stopped) Context.Finish(CastOutcome.Cancelled);
            yield break;
        }
        if (Actor.PlayerMovement == null)
            finalPoint = new Vector3(Actor.transform.position.x, takeoff.y, Actor.transform.position.z);

        uniteLanding = true;
        character.StopEffect(warningVisual);
        warningVisual = character.ShowEffect(CresseliaEffect.UniteWarning, finalPoint, Actor.transform.forward,
            CresseliaMoveRules.LandingDuration, 0, CresseliaMoveRules.UniteRadius);

        IDisposable landingMovement = Context.Scope.Own(character.AcquireMovementLock());
        IDisposable landingLock = Context.Scope.Own(new CombatActionLock(Actor, true, lockServer: true, lockMoves: false));
        Vector3 descentOrigin = Actor.transform.position;
        double landingStarted = character.CurrentTime;
        animation = "pm0488_ba01_land01";
        Actor.AnimationManager?.SetFloat("CresseliaActionSpeed", 2f);
        Actor.AnimationManager?.PlayAnimation(animation);
        character.SetUnitePhase(2, finalPoint, descentOrigin, landingStarted);

        while (Alive && character.CurrentTime < landingStarted + CresseliaMoveRules.LandingDuration)
            yield return null;

        landingMovement.Dispose();
        landingLock.Dispose();
        if (!Alive)
        {
            if (!stopped) Context.Finish(CastOutcome.Interrupted);
            yield break;
        }

        uniteLanded = true;
        Actor.transform.position = finalPoint;
        foreach (Pokemon target in CresseliaMoveRuntime.Targets(Physics.OverlapSphere(finalPoint, CresseliaMoveRules.UniteRadius,
            LayerMask.GetMask("Players", "WildPokemons"))))
        {
            if (!CresseliaMoveRuntime.CanHit(Actor, target)) continue;
            target.TakeDamageRPC(new DamageInfo(Controller.NetworkObjectId, 1.35f, 9, 840, DamageType.Special, DamageProprieties.IsUniteMove));
            if (CresseliaMoveRuntime.Living(target))
                target.AddStatusEffect(new StatusEffect(StatusType.Asleep, 1.2f, true, 0));
        }
        character.ShowEffect(CresseliaEffect.UniteImpact, finalPoint, Vector3.forward, 0.55f, 0, CresseliaMoveRules.UniteRadius);
        if (!stopped)
            Context.Finish(CastOutcome.Completed);
    }

    private void Face(Vector3 value)
    {
        value.y = 0f;
        if (value.sqrMagnitude > 0.001f)
            Actor.transform.rotation = Quaternion.LookRotation(value);
    }

    public override void Stop(CastOutcome reason)
    {
        if (stopped) return;
        stopped = true;
        followUpReady = false;
        routine?.Dispose();
        routine = null;
    }
}
