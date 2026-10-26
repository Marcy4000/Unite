using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class FlareonMoveBase : MoveBase
{
    public abstract FlareonCast Kind { get; }
    private readonly List<IDisposable> leases = new List<IDisposable>();
    private readonly WallCrossingTraversal traversal = new WallCrossingTraversal();
    private FlareonCast executingKind;
    private Vector3 direction, velocity;
    private Pokemon target;
    private bool pending, executing, rooted, movementEnded;
    private float elapsed, nextReport, drivenDistance;
    private int cast;
    public Vector3 AimDirection => direction;
    public bool CanBuffer => executing && FlareonMoveRules.Recovery(executingKind) - elapsed <= FlareonMoveRules.InputBuffer;
    public bool CanRecast => Kind == FlareonCast.Takedown && !pending && !executing && playerManager != null && playerManager.MovesController.FlareonRecastAvailable;
    private bool CrossesWalls => executingKind == FlareonCast.Tackle || executingKind == FlareonCast.Slam || executingKind == FlareonCast.Unite;

    public override void Start(PlayerManager controller)
    {
        if (pending || executing) return;
        base.Start(controller);
        executingKind = CanRecast ? FlareonCast.Slam : Kind;
        if (FlareonMoveRules.Targeted(executingKind)) Aim.Instance.InitializeAutoAim(FlareonMoveRules.Range(executingKind), 60, AimTarget.NonAlly);
        else if (executingKind == FlareonCast.HeatWave || executingKind == FlareonCast.Swift) Aim.Instance.InitializeSimpleCircle(FlareonMoveRules.Range(executingKind));
        else if (FlareonMoveRules.Driven(executingKind)) Aim.Instance.InitializeDashAim(FlareonMoveRules.Range(executingKind));
        else Aim.Instance.InitializeSkillshotAim(FlareonMoveRules.Range(executingKind));
        ReadAim();
    }
    private void ReadAim()
    {
        if (FlareonMoveRules.Targeted(executingKind))
        {
            target = executingKind == FlareonCast.Slam ? playerManager.MovesController.FlareonRecastTarget : Aim.Instance.SureHitAim()?.GetComponent<Pokemon>();
            direction = FlareonMoveRules.Direction(target != null ? target.transform.position - playerManager.transform.position : Vector3.zero, playerManager.transform.forward);
        }
        else direction = FlareonMoveRules.Direction(FlareonMoveRules.Driven(executingKind) ? Aim.Instance.DashAim()
            : executingKind == FlareonCast.Flamethrower ? Aim.Instance.SkillshotAim() : playerManager.transform.forward, playerManager.transform.forward);
    }
    public override void Finish()
    {
        if (!IsActive || pending || executing) return;
        ReadAim();
        if (FlareonMoveRules.Targeted(executingKind) && target == null) { Cancel(); return; }
        HideAim(); IsActive = false; pending = true; elapsed = 0;
        cast = playerManager.MovesController.NextFlareonCast(); Acquire();
        playerManager.MovesController.CastFlareonRPC(executingKind, cast, direction, target != null ? target.NetworkObjectId : 0);
    }
    public void Accept(int id, bool accepted, Vector3 destination)
    {
        if (!pending || cast != id) return;
        pending = false;
        if (!accepted) { Complete(); return; }
        executing = true; elapsed = nextReport = drivenDistance = 0; movementEnded = false;
        if (CrossesWalls) traversal.Begin(playerManager.PlayerMovement);
        velocity = FlareonMoveRules.Targeted(executingKind) ? (destination - playerManager.transform.position) / FlareonMoveRules.DriveTime(executingKind)
            : direction * (FlareonMoveRules.Range(executingKind) / FlareonMoveRules.DriveTime(executingKind));
        if (executingKind == FlareonCast.Tackle)
            velocity = (playerManager.PlayerMovement.GetWallCrossingDestination(playerManager.transform.position + direction * FlareonMoveRules.Range(executingKind))
                - playerManager.transform.position) / FlareonMoveRules.DriveTime(executingKind);
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        PlayMoveAnimation();
        if (executingKind != FlareonCast.Slam)
        {
            wasMoveSuccessful = true; base.Finish();
        }
    }
    private void PlayMoveAnimation()
    {
        string clip = executingKind == FlareonCast.Unite ? "pm0136_ba01_uniteMove" : executingKind == FlareonCast.Slam ? "pm0136_ba01_landA01"
            : executingKind == FlareonCast.Flamethrower ? "pm0136_ba21_tokusyu01" : executingKind == FlareonCast.HeatWave ? "pm0136_ba02_roar01"
            : executingKind == FlareonCast.Tackle ? "ani_leafeonspell2_bat_0133" : "pm0136_fi21_run01";
        Animator animator = playerManager.AnimationManager.Animator;
        if (animator != null && animator.HasState(0, Animator.StringToHash(clip))) playerManager.AnimationManager.PlayAnimation(clip);
    }
    public override void Update()
    {
        if (IsActive) { ReadAim(); return; }
        if (!pending && !executing) return;
        if (playerManager.Pokemon.CurrentHp <= 0 || playerManager.Pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts)) { ResetMove(); return; }
        float previous = elapsed; elapsed += Time.deltaTime;
        if (pending) { if (elapsed >= 5) ResetMove(); return; }
        if (FlareonMoveRules.Driven(executingKind) && !movementEnded)
        {
            if (playerManager.Pokemon.HasStatusEffect(StatusType.Immobilized)) { ResetMove(); return; }
            float start = FlareonMoveRules.Windup(executingKind), end = start + FlareonMoveRules.DriveTime(executingKind);
            float delta = Mathf.Max(0, Mathf.Min(elapsed, end) - Mathf.Max(previous, start));
            Vector3 step = velocity * delta;
            if (delta > 0 && FlareonMoveRules.Targeted(executingKind) && target != null && target.IsSpawned && target.CurrentHp > 0)
            {
                Vector3 goal = target.transform.position - FlareonMoveRules.Direction(target.transform.position - playerManager.transform.position, direction) * 0.65f;
                // Do not stop inside a wall just because the target is within
                // melee reach on its far side. Range and travel time stay capped.
                if (CrossesWalls && !playerManager.PlayerMovement.IsWallCrossingLandingValid(goal)) goal = target.transform.position;
                Vector3 toward = goal - playerManager.transform.position; toward.y = 0;
                direction = FlareonMoveRules.Direction(toward, direction);
                float remainingRange = Mathf.Max(0, FlareonMoveRules.Range(executingKind) - drivenDistance);
                step = direction * Mathf.Min(toward.magnitude, remainingRange, FlareonMoveRules.Range(executingKind) / FlareonMoveRules.DriveTime(executingKind) * delta);
                playerManager.transform.rotation = Quaternion.LookRotation(direction);
            }
            Vector3 actual = delta > 0 ? (CrossesWalls ? traversal.Move(step) : playerManager.PlayerMovement.MoveWithWalls(step)) : Vector3.zero;
            drivenDistance += actual.magnitude;
            bool blocked = delta > 0 && step.sqrMagnitude > 0.0001f && actual.sqrMagnitude < step.sqrMagnitude * 0.02f;
            if (elapsed >= end || blocked || FlareonMoveRules.Targeted(executingKind) && drivenDistance >= FlareonMoveRules.Range(executingKind))
            {
                movementEnded = true;
                traversal.End();
                playerManager.MovesController.FlareonMovementRPC(cast, true, playerManager.transform.position);
            }
            else if (delta > 0 && elapsed >= nextReport)
            {
                nextReport = elapsed + 0.04f;
                playerManager.MovesController.FlareonMovementRPC(cast, false, playerManager.transform.position);
            }
        }
        if (elapsed >= FlareonMoveRules.Recovery(executingKind)) Complete();
    }
    private void Acquire()
    {
        rooted = true; playerManager.PlayerMovement.AddMovementRestriction();
        for (int i = 0; i < 3; i++) leases.Add(playerManager.MovesController.AcquireMoveStatus(i, ActionStatusType.Busy));
        leases.Add(playerManager.MovesController.BasicAttackStatus.AcquireStatus(ActionStatusType.Busy));
        leases.Add(playerManager.MovesController.BattleItemStatus.AcquireStatus(ActionStatusType.Busy));
        leases.Add(playerManager.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
    }
    private void Complete()
    {
        bool hadAction = rooted || executing || pending;
        traversal.End();
        foreach (IDisposable lease in leases) lease.Dispose(); leases.Clear();
        if (rooted && playerManager != null) playerManager.PlayerMovement.RemoveMovementRestriction();
        rooted = pending = executing = false; target = null;
        if (hadAction && playerManager != null && playerManager.IsSpawned && playerManager.NetworkManager != null && playerManager.NetworkManager.IsListening && playerManager.Pokemon.CurrentHp > 0
            && !playerManager.Pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts)) playerManager.AnimationManager.SetTrigger("Transition");
    }
    private void HideAim()
    {
        if (Aim.Instance == null) return;
        if (FlareonMoveRules.Targeted(executingKind)) Aim.Instance.HideAutoAim();
        else if (executingKind == FlareonCast.HeatWave || executingKind == FlareonCast.Swift) Aim.Instance.HideSimpleCircle();
        else if (FlareonMoveRules.Driven(executingKind)) Aim.Instance.HideDashAim();
        else Aim.Instance.HideSkillshotAim();
    }
    // Cancelling aim does not delete a committed aura. Hard interruption does cancel a running action.
    public override void Cancel()
    {
        if (IsActive) HideAim(); base.Cancel();
        if ((pending || executing) && playerManager != null && playerManager.Pokemon.HasAnyStatusEffect(FlareonMoveRules.Interrupts)) ResetMove();
    }
    public override void ResetMove()
    {
        if (IsActive) HideAim(); IsActive = false;
        if (playerManager != null && playerManager.IsSpawned && playerManager.NetworkManager != null && playerManager.NetworkManager.IsListening) playerManager.MovesController.CancelFlareonMoveRPC(Kind);
        Complete();
    }
}
