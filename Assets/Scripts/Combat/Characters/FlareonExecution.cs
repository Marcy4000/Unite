using System;
using System.Collections;
using Unite.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;

public sealed class FlareonExecution : CombatExecution
{
    private readonly FlareonCharacterRuntime character;
    private readonly FlareonCast kind;
    private readonly WallCrossingTraversal traversal = new WallCrossingTraversal();
    private Vector3 direction;
    private GameObject prefab;
    private GameObject slamPrefab;
    private FlareonMoveObject effect;
    private CombatRoutine routine;
    private IDisposable actionLock;
    private IDisposable movementLock;
    private bool stopped;
    private bool followUpRequested;
    private bool driveInterrupted;
    private bool traversing;
    private int traversalSequence;
    private string animation;

    public FlareonExecution(CombatExecutionContext context, FlareonCharacterRuntime character) : base(context)
    {
        this.character = character;
        kind = FlareonCharacterRuntime.KindForMove(context.MoveId);
        direction = FlareonMoveRules.Direction(context.Direction, context.Actor.transform.forward);
    }

    public override bool FollowUpReady => !stopped && !followUpRequested && kind == FlareonCast.Takedown && character.TakedownRecastAvailable;
    public override Pokemon FollowUpTarget => FollowUpReady ? character.RecastTarget : null;

    public override IEnumerator Prepare()
    {
        Prepared = false;
        if (!Actor.IsServer || kind == FlareonCast.State) yield break;
        yield return Controller.Combat.Spawner.Load(FlareonMoveRules.Path(kind), Context.Scope, loaded => prefab = loaded);
        if (Context.Scope.IsDisposed || prefab == null || prefab.GetComponent<NetworkObject>() == null || prefab.GetComponent<FlareonMoveObject>() == null) yield break;
        if (kind == FlareonCast.Takedown)
        {
            yield return Controller.Combat.Spawner.Load(FlareonMoveRules.Path(FlareonCast.Slam), Context.Scope, loaded => slamPrefab = loaded);
            if (Context.Scope.IsDisposed || slamPrefab == null || slamPrefab.GetComponent<NetworkObject>() == null || slamPrefab.GetComponent<FlareonMoveObject>() == null) yield break;
        }
        Prepared = true;
    }

    public override void Start()
    {
        if (!Prepared || stopped || !Actor.IsServer || Context.Scope.IsDisposed) return;
        routine = Context.Scope.Own(new CombatRoutine(Actor, Execute()));
    }

    private bool Alive => !stopped && Actor != null && Actor.IsSpawned && Actor.Pokemon != null && Actor.Pokemon.CurrentHp > 0;
    private bool Interrupted => !Alive || Actor.Pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts)
        || (actionLock != null && (effect == null || !effect.IsSpawned));

    private IEnumerator Execute()
    {
        switch (Context.MoveId)
        {
            case AvailableMoves.FlareonSwift: yield return ExecuteSwift(); break;
            case AvailableMoves.FlareonTackle: yield return ExecuteTackle(); break;
            case AvailableMoves.FlareonHeatWave: yield return ExecuteHeatWave(); break;
            case AvailableMoves.FlareonTakedown: yield return ExecuteTakedown(); break;
            case AvailableMoves.FlareonFlameThrower: yield return ExecuteFlamethrower(); break;
            case AvailableMoves.FlareonFlareBlitz: yield return ExecuteFlareBlitz(); break;
            case AvailableMoves.FlareonUnite: yield return ExecuteUnite(); break;
        }
    }

    private IEnumerator ExecuteSwift()
    {
        if (!BeginCast(FlareonCast.Swift, prefab, "pm0136_fi21_run01")) yield break;
        character.StartSwiftField(effect);
        yield return Wait(FlareonMoveRules.Recovery(FlareonCast.Swift));
        FinishCast();
    }

    private IEnumerator ExecuteTackle()
    {
        if (!BeginCast(FlareonCast.Tackle, prefab, "ani_leafeonspell2_bat_0133")) yield break;
        yield return Traverse(FlareonCast.Tackle, null);
        if (!Interrupted && !driveInterrupted)
        {
            effect.SetCombatPhase(1);
            character.EndEffectAfter(effect, 0.2f);
        }
        yield return Wait(0.08f);
        FinishCast();
    }

    private IEnumerator ExecuteHeatWave()
    {
        if (!BeginCast(FlareonCast.HeatWave, prefab, "pm0136_ba02_roar01")) yield break;
        character.StartHeatWaveField(effect, Context.Upgraded);
        yield return Wait(FlareonMoveRules.Recovery(FlareonCast.HeatWave));
        FinishCast();
    }

    private IEnumerator ExecuteTakedown()
    {
        if (!BeginCast(FlareonCast.Takedown, prefab, "pm0136_fi21_run01")) yield break;
        yield return Traverse(FlareonCast.Takedown, Context.Target);
        if (Interrupted || driveInterrupted) { FinishCast(); yield break; }
        effect.SetCombatPhase(1);
        if (effect.LandCombatTakedown(character, false))
            character.ArmTakedownRecast(Context.Target, FlareonMoveRules.LaunchDuration(Context.Upgraded) - 0.08f);
        character.EndEffectAfter(effect, 0.2f);
        yield return Wait(0.08f);
        if (Interrupted || driveInterrupted) { FinishCast(); yield break; }
        ReleaseLocks();
        RecoverAnimation();
        Controller.Combat.PublishSlot(Context.Slot);
        while (!Interrupted && character.TakedownRecastAvailable && !followUpRequested) yield return null;
        if (Interrupted || !followUpRequested)
        {
            character.ClearTakedownRecast();
            FinishCast();
            yield break;
        }
        Pokemon victim = character.RecastTarget;
        character.ClearTakedownRecast();
        if (!FlareonMoveRules.CanHit(Actor, victim)) { FinishCast(); yield break; }
        if (!BeginCast(FlareonCast.Slam, slamPrefab, "pm0136_ba01_landA01", victim)) yield break;
        yield return Traverse(FlareonCast.Slam, victim);
        if (!Interrupted && !driveInterrupted)
        {
            effect.SetCombatPhase(1);
            effect.LandCombatTakedown(character, true);
            character.EndEffectAfter(effect, 0.2f);
        }
        yield return Wait(0.08f);
        FinishCast();
    }

    private IEnumerator ExecuteFlamethrower()
    {
        if (!BeginCast(FlareonCast.Flamethrower, prefab, "pm0136_ba21_tokusyu01")) yield break;
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, 0.65f, true, FlareonMoveRules.CastProtection));
        if (Context.Upgraded) effect.GrantCombatShield(FlareonMoveRules.FlameShield, 0.1f, 4);
        yield return Wait(0.2f);
        if (Interrupted) { FinishCast(); yield break; }
        effect.SetCombatPhase(1);
        float started = Time.time;
        while (!Interrupted && Time.time - started < 0.4f)
        {
            effect.MoveCombatObject(Actor.transform.position, direction);
            effect.SweepCombatFlame(character, (Time.time - started) / 0.4f);
            yield return null;
        }
        if (!Interrupted)
        {
            effect.SetCombatPhase(2);
            character.EndEffectAfter(effect, 0.12f);
        }
        yield return Wait(0.05f);
        FinishCast();
    }

    private IEnumerator ExecuteFlareBlitz()
    {
        if (!BeginCast(FlareonCast.FlareBlitz, prefab, "pm0136_fi21_run01")) yield break;
        yield return Wait(FlareonMoveRules.Windup(FlareonCast.FlareBlitz));
        if (!Interrupted) yield return Traverse(FlareonCast.FlareBlitz, null);
        if (!Interrupted && !driveInterrupted)
        {
            effect.SetCombatPhase(1);
            character.EndEffectAfter(effect, 0.2f);
        }
        yield return Wait(0.08f);
        FinishCast();
    }

    private IEnumerator ExecuteUnite()
    {
        if (!BeginCast(FlareonCast.Unite, prefab, "pm0136_ba01_uniteMove")) yield break;
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, 0.65f, true, FlareonMoveRules.CastProtection));
        yield return Wait(FlareonMoveRules.Windup(FlareonCast.Unite));
        if (!Interrupted) yield return Traverse(FlareonCast.Unite, Context.Target);
        if (!Interrupted && !driveInterrupted)
        {
            effect.SetCombatPhase(1);
            Context.MarkCastSucceeded();
            effect.LandCombatUnite(character);
            character.EndEffectAfter(effect, 0.4f);
            Actor.AnimationManager?.SetTrigger("Transition");
        }
        yield return Wait(0.08f);
        FinishCast();
    }

    private bool BeginCast(FlareonCast move, GameObject preparedPrefab, string moveAnimation, Pokemon target = null)
    {
        if (Interrupted) { FinishCast(); return false; }
        actionLock = Context.Scope.Own(new CombatActionLock(Actor, false));
        movementLock = Context.Scope.Own(character.AcquireMovementLock());
        GameObject spawned = Controller.Combat.Spawner.Spawn(preparedPrefab, Actor.transform.position, Quaternion.LookRotation(direction), Context.Scope, false);
        effect = spawned != null ? spawned.GetComponent<FlareonMoveObject>() : null;
        if (effect == null)
        {
            if (spawned != null && spawned.TryGetComponent(out NetworkObject networkObject) && networkObject.IsSpawned) networkObject.Despawn(true);
            ReleaseLocks();
            Context.Finish(CastOutcome.ExecutionFailed);
            return false;
        }
        effect.HoldAsset(Addressables.LoadAssetAsync<GameObject>(FlareonMoveRules.Path(move)));
        effect.InitializeCombat(Actor, move, direction, target != null ? target : Context.Target, Context.Upgraded);
        character.TrackEffect(effect, Context.Slot);
        character.BeginRecovery(Context.CastId, FlareonMoveRules.Recovery(move));
        animation = moveAnimation;
        Actor.transform.rotation = Quaternion.LookRotation(direction);
        Actor.AnimationManager?.PlayAnimation(animation);
        return true;
    }

    private IEnumerator Traverse(FlareonCast move, Pokemon target)
    {
        driveInterrupted = false;
        traversing = true;
        bool crossesWalls = CrossesWalls(move);
        if (crossesWalls) traversal.Begin(Actor.PlayerMovement);
        Vector3 origin = Actor.transform.position;
        Vector3 requested = origin + direction * FlareonMoveRules.Range(move);
        if (move == FlareonCast.Tackle) requested = Actor.PlayerMovement.GetWallCrossingDestination(requested);
        Vector3 velocity = (requested - origin) / FlareonMoveRules.DriveTime(move);
        Vector3 authoritativePosition = origin;
        float elapsed = 0;
        float distance = 0;
        bool cooldownRefunded = false;
        try
        {
            while (!Interrupted && !Actor.Pokemon.HasStatusEffect(StatusType.Immobilized) && elapsed < FlareonMoveRules.DriveTime(move))
            {
                Actor.transform.position = authoritativePosition;
                float delta = Mathf.Min(Time.deltaTime, FlareonMoveRules.DriveTime(move) - elapsed);
                elapsed += delta;
                Vector3 step = velocity * delta;
                if (FlareonMoveRules.Targeted(move) && target != null && target.IsSpawned && target.CurrentHp > 0)
                {
                    Vector3 goal = target.transform.position - FlareonMoveRules.Direction(target.transform.position - Actor.transform.position, direction) * 0.65f;
                    if (crossesWalls && !Actor.PlayerMovement.IsWallCrossingLandingValid(goal)) goal = target.transform.position;
                    Vector3 toward = goal - Actor.transform.position;
                    toward.y = 0;
                    direction = FlareonMoveRules.Direction(toward, direction);
                    float remaining = Mathf.Max(0, FlareonMoveRules.Range(move) - distance);
                    step = direction * Mathf.Min(toward.magnitude, remaining, FlareonMoveRules.Range(move) / FlareonMoveRules.DriveTime(move) * delta);
                    Actor.transform.rotation = Quaternion.LookRotation(direction);
                }
                Vector3 previous = Actor.transform.position;
                Vector3 actual = crossesWalls ? traversal.Move(step) : Actor.PlayerMovement.MoveWithWalls(step);
                distance += actual.magnitude;
                Vector3 point = Actor.transform.position;
                authoritativePosition = point;
                if (move == FlareonCast.Tackle || move == FlareonCast.FlareBlitz)
                    cooldownRefunded = effect.SweepCombatTravel(character, move, previous, point, cooldownRefunded);
                effect.MoveCombatObject(point, direction);
                Controller.FlareonServerPositionRPC(Context.CastId, ++traversalSequence, point, direction);
                if (step.sqrMagnitude > 0.0001f && actual.sqrMagnitude < step.sqrMagnitude * 0.02f) break;
                if (FlareonMoveRules.Targeted(move) && distance >= FlareonMoveRules.Range(move)) break;
                yield return null;
            }
            driveInterrupted = Interrupted || Actor.Pokemon.HasStatusEffect(StatusType.Immobilized);
        }
        finally
        {
            CloseTraversal();
        }
    }

    private static bool CrossesWalls(FlareonCast move)
        => move == FlareonCast.Tackle || move == FlareonCast.Slam || move == FlareonCast.Unite;

    private void CloseTraversal()
    {
        if (!traversing) return;
        traversing = false;
        bool canLand = Actor != null && Actor.IsSpawned && Actor.Pokemon != null && Actor.Pokemon.CurrentHp > 0;
        if (canLand) traversal.End(); else traversal.Cancel();
        if (!canLand) return;
        if (effect != null && effect.IsSpawned) effect.MoveCombatObject(Actor.transform.position, direction);
        Controller.FlareonServerPositionRPC(Context.CastId, ++traversalSequence, Actor.transform.position, direction);
    }

    private IEnumerator Wait(float duration)
    {
        float until = Time.time + duration;
        while (!Interrupted && Time.time < until) yield return null;
    }

    public override bool TryRecast(Vector3 requestedDirection, ulong targetId)
    {
        if (!FollowUpReady || !FlareonMoveRules.Finite(requestedDirection)) return false;
        Pokemon target = character.RecastTarget;
        if (target == null || target.NetworkObjectId != targetId
            || Vector3.Distance(Actor.transform.position, target.transform.position) > FlareonMoveRules.Range(FlareonCast.Slam) + 0.4f
            || Actor.Pokemon.HasStatusEffect(StatusType.Immobilized)) return false;
        followUpRequested = true;
        direction = FlareonMoveRules.Direction(target.transform.position - Actor.transform.position, direction);
        Controller.Combat.PublishSlot(Context.Slot);
        return true;
    }

    private void ReleaseLocks()
    {
        character.EndRecovery(Context.CastId);
        actionLock?.Dispose();
        actionLock = null;
        movementLock?.Dispose();
        movementLock = null;
    }

    private void RecoverAnimation()
    {
        if (!Interrupted && !string.IsNullOrEmpty(animation))
            Actor.AnimationManager?.RecoverAnimationRPC(animation, Actor.Pokemon.CurrentLevel < 3 ? "ani_idle_bat_0133" : "pm0136_ba10_waitA01", 0.06f);
    }

    private void FinishCast()
    {
        ReleaseLocks();
        if (!Interrupted && !driveInterrupted) RecoverAnimation();
        if (!stopped) Context.Finish(Interrupted || driveInterrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    public override void Stop(CastOutcome reason)
    {
        if (stopped) return;
        stopped = true;
        routine?.Dispose();
        routine = null;
        CloseTraversal();
        ReleaseLocks();
        if (kind == FlareonCast.Takedown) character.ClearTakedownRecast();
        if (reason != CastOutcome.Completed && effect != null && effect.IsSpawned) effect.Despawn();
    }
}
