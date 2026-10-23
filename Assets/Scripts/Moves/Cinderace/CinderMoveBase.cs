using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class CinderMoveBase : MoveBase
{
    public abstract CinderCast Kind { get; }
    private readonly List<IDisposable> locks = new List<IDisposable>();
    private Vector3 direction, approach;
    private Pokemon target;
    private bool pending, executing, rooted, movementDone, rebounding;
    private float elapsed;
    private float phaseDistance, phaseProgress;
    private Vector3 phaseDirection;
    private int cast;
    public int CastId => cast;

    protected CinderMoveBase() { Cooldown = CinderMoveRules.Cooldown(Kind); }
    public override void Start(PlayerManager controller)
    {
        if (pending || executing) return;
        base.Start(controller);
        if (CinderMoveRules.Targeted(Kind)) Aim.Instance.InitializeAutoAim(CinderMoveRules.Range(Kind), 90, AimTarget.NonAlly);
        else if (CinderMoveRules.Dash(Kind)) Aim.Instance.InitializeDashAim(CinderMoveRules.DashDistance);
        else if (Kind == CinderCast.PyroBall) Aim.Instance.InitializeSkillshotAim(CinderMoveRules.Range(Kind));
        ReadAim();
    }
    private void ReadAim()
    {
        if (CinderMoveRules.Targeted(Kind))
        {
            var go = Aim.Instance.SureHitAim(); target = go != null ? go.GetComponent<Pokemon>() : null;
            direction = target != null ? target.transform.position - playerManager.transform.position : playerManager.transform.forward;
        }
        else if (CinderMoveRules.Dash(Kind)) direction = Aim.Instance.DashAim();
        else if (Kind == CinderCast.PyroBall) direction = Aim.Instance.SkillshotAim();
        else direction = playerManager.transform.forward;
        direction = CinderMoveRules.Direction(direction, playerManager.transform.forward);
    }
    public override void Finish()
    {
        if (!IsActive || pending || executing) return;
        ReadAim(); HideAim(); IsActive = false;
        if (CinderMoveRules.Targeted(Kind) && !CinderMoveRules.CanHit(playerManager, target)) { base.Finish(); return; }
        pending = true; elapsed = 0; movementDone = false; rebounding = false;
        cast = playerManager.MovesController.NextCinderaceCast();
        Acquire();
        playerManager.MovesController.CastCinderaceRPC(Kind, cast, direction, target != null ? target.NetworkObjectId : ulong.MaxValue);
    }
    public void Accept(int id, bool accepted)
    {
        if (!pending || cast != id) return;
        pending = false;
        if (!accepted) { Complete(); return; }
        executing = true; elapsed = 0;
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        string animation = Kind == CinderCast.Ember ? "ani_spell1_bat_0815" : Kind == CinderCast.PyroBall ? "ani_spell1a_bat_0815"
            : Kind == CinderCast.BlazeKick ? "ani_spell1b1_bat_0815" : Kind == CinderCast.LowSweep ? "ani_spell2_bat_0815"
            : Kind == CinderCast.FlameCharge ? "ani_spell2a_bat_0815" : Kind == CinderCast.Feint ? "ani_spell2b_bat_0815" : "ani_spellu_bat_0815";
        playerManager.AnimationManager.PlayAnimation(animation);
        if (CinderMoveRules.Dash(Kind)) BeginDrive(direction, CinderMoveRules.DashDistance);
        if (Kind == CinderCast.BlazeKick && target != null)
        {
            approach = CinderMoveRules.Direction(target.transform.position - playerManager.transform.position, direction);
            float distance = Mathf.Max(0, Vector3.Distance(target.transform.position, playerManager.transform.position) - 0.8f);
            BeginDrive(approach, distance);
        }
        wasMoveSuccessful = true; base.Finish();
    }
    public override void Update()
    {
        if (IsActive) { ReadAim(); return; }
        if (!pending && !executing) return;
        elapsed += Time.deltaTime;
        if (playerManager.Pokemon.CurrentHp <= 0 || ((pending || (Kind != CinderCast.BlazeKick && Kind != CinderCast.Unite))
            && playerManager.Pokemon.HasAnyStatusEffect(CinderMoveRules.Interrupts))
            || (CinderMoveRules.Dash(Kind) && playerManager.Pokemon.HasStatusEffect(StatusType.Immobilized))) { ResetMove(); return; }
        if (pending) { if (elapsed > 10) ResetMove(); return; }
        if (Kind == CinderCast.BlazeKick)
        {
            Drive(rebounding ? CinderMoveRules.KickRebound : CinderMoveRules.KickApproach);
            if (!rebounding && elapsed >= CinderMoveRules.KickApproach)
            {
                rebounding = true; elapsed = 0;
                playerManager.MovesController.CinderaceMovementPhaseRPC(cast, false, playerManager.transform.position);
                playerManager.AnimationManager.PlayAnimation("ani_spell1b2_bat_0815");
                Vector2 input = playerManager.MovesController.Controls.Movement.Move.ReadValue<Vector2>();
                Vector3 rebound = CinderMoveRules.Direction(new Vector3(input.x, 0, input.y), -approach);
                BeginDrive(rebound, CinderMoveRules.ReboundDistance);
            }
            if (rebounding && elapsed >= CinderMoveRules.KickRebound)
            {
                playerManager.MovesController.CinderaceMovementPhaseRPC(cast, true, playerManager.transform.position); Complete();
            }
        }
        else if (CinderMoveRules.Dash(Kind))
        {
            Drive(CinderMoveRules.DashDuration);
            if (!movementDone && elapsed >= CinderMoveRules.DashDuration)
            {
                movementDone = true; playerManager.MovesController.CinderaceMovementPhaseRPC(cast, true, playerManager.transform.position); Complete();
            }
        }
        else if (elapsed >= CinderMoveRules.Recovery(Kind)) Complete();
    }
    private void BeginDrive(Vector3 aim, float distance) { phaseDirection = aim; phaseDistance = distance; phaseProgress = 0; }
    private void Drive(float duration)
    {
        float progress = Mathf.Clamp01(elapsed / duration);
        playerManager.PlayerMovement.CinderaceDashStep(phaseDirection * (phaseDistance * (progress - phaseProgress)));
        phaseProgress = progress;
    }
    private void Acquire()
    {
        rooted = Kind != CinderCast.Feint;
        if (rooted) playerManager.PlayerMovement.AddMovementRestriction();
        for (int i = 0; i < 3; i++) locks.Add(playerManager.MovesController.AcquireMoveStatus(i, ActionStatusType.Disabled));
        locks.Add(playerManager.MovesController.BasicAttackStatus.AcquireStatus(ActionStatusType.Disabled));
        locks.Add(playerManager.MovesController.BattleItemStatus.AcquireStatus(ActionStatusType.Disabled));
        locks.Add(playerManager.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
    }
    private void Complete()
    {
        if (executing && phaseDistance > 0 && playerManager != null) playerManager.PlayerMovement.FinishCinderaceTraversal();
        phaseDistance = 0;
        pending = executing = false;
        foreach (IDisposable lease in locks) lease.Dispose(); locks.Clear();
        if (rooted && playerManager != null) playerManager.PlayerMovement.RemoveMovementRestriction();
        rooted = false;
        if (playerManager != null && playerManager.Pokemon.CurrentHp > 0 && !playerManager.Pokemon.HasAnyStatusEffect(CinderMoveRules.Interrupts))
            playerManager.AnimationManager.SetTrigger("Transition");
    }
    private void HideAim()
    {
        if (Aim.Instance == null) return;
        if (CinderMoveRules.Targeted(Kind)) Aim.Instance.HideAutoAim();
        else if (CinderMoveRules.Dash(Kind)) Aim.Instance.HideDashAim();
        else if (Kind == CinderCast.PyroBall) Aim.Instance.HideSkillshotAim();
    }
    public override void Cancel() { if (IsActive) HideAim(); base.Cancel(); }
    public override void ResetMove()
    {
        Cancel();
        if ((pending || executing) && playerManager != null && playerManager.IsSpawned) playerManager.MovesController.CancelCinderaceCastRPC(cast);
        Complete(); target = null;
    }
}
