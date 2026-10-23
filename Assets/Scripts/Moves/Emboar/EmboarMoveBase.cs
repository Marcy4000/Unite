using System;
using System.Collections.Generic;
using UnityEngine;

// Owner-only input and wall-safe movement. Damage, empowerment and effect lifetimes are server-owned.
public abstract class EmboarMoveBase : MoveBase
{
    public abstract EmboarCast Kind { get; }
    private readonly List<IDisposable> leases = new List<IDisposable>();
    private IDisposable ownSlot;
    private Vector3 direction, destination, travelVelocity;
    private ulong targetId;
    private bool hasTarget, pending, executing, rooted, movementEnded, empowered, recast;
    private float elapsed, nextReport, recovery;
    private int cast;
    public bool CanBrake => Kind == EmboarCast.FlameCharge && executing && !movementEnded;
    public bool CanBuffer => executing && recovery - elapsed <= EmboarMoveRules.InputBuffer;

    public override void Start(PlayerManager controller)
    {
        if (CanBrake) { recast = true; return; }
        if (pending || executing) return;
        base.Start(controller);
        if (Kind == EmboarCast.Ember) Aim.Instance.InitializeAutoAim(EmboarMoveRules.Range(Kind), 60, AimTarget.NonAlly);
        else if (Kind == EmboarCast.HeatCrash || Kind == EmboarCast.Unite)
            Aim.Instance.InitializeSizedCircleAreaIndicator(EmboarMoveRules.Range(Kind), Kind == EmboarCast.Unite ? EmboarMoveRules.UniteRadius : EmboarMoveRules.CrashRadius);
        else if (Kind != EmboarCast.FirePledge) Aim.Instance.InitializeSkillshotAim(EmboarMoveRules.Range(Kind));
        ReadAim();
    }
    private void ReadAim()
    {
        Vector3 forward = playerManager.transform.forward;
        if (Kind == EmboarCast.Ember)
        {
            GameObject target = Aim.Instance.SureHitAim();
            hasTarget = target != null && target.TryGetComponent<Unity.Netcode.NetworkObject>(out _);
            targetId = hasTarget ? target.GetComponent<Unity.Netcode.NetworkObject>().NetworkObjectId : 0;
            direction = hasTarget ? EmboarMoveRules.Direction(target.transform.position - playerManager.transform.position, forward) : forward;
        }
        else if (Kind == EmboarCast.HeatCrash || Kind == EmboarCast.Unite)
        {
            destination = EmboarMoveRules.Destination(playerManager.transform.position, Aim.Instance.CircleAreaAim(), EmboarMoveRules.Range(Kind));
            direction = EmboarMoveRules.Direction(destination - playerManager.transform.position, forward);
        }
        else direction = EmboarMoveRules.Direction(Kind == EmboarCast.FirePledge ? forward : Aim.Instance.SkillshotAim(), forward);
    }
    public override void Finish()
    {
        if (recast && CanBrake) { recast = false; EndMovement(); return; }
        if (!IsActive || pending || executing) return;
        ReadAim();
        if (Kind == EmboarCast.Ember && !hasTarget) { Cancel(); return; }
        HideAim(); IsActive = false;
        cast = playerManager.MovesController.NextEmboarCast(); pending = true; elapsed = 0; movementEnded = false;
        Acquire();
        playerManager.MovesController.CastEmboarRPC(Kind, cast, direction, destination, targetId);
    }
    public void Accept(int id, bool accepted, bool boost, Vector3 point)
    {
        if (!pending || id != cast) return;
        pending = false;
        if (!accepted) { Complete(); return; }
        executing = true; empowered = boost; elapsed = nextReport = 0; destination = point;
        recovery = EmboarMoveRules.Recovery(Kind);
        playerManager.transform.rotation = Quaternion.LookRotation(Kind == EmboarCast.Smog ? -direction : direction);
        if (Kind == EmboarCast.HeatCrash) travelVelocity = (destination - playerManager.transform.position) / EmboarMoveRules.CrashLeap;
        else if (Kind == EmboarCast.Unite) travelVelocity = (destination - playerManager.transform.position) / EmboarMoveRules.UniteDrive;
        else if (Kind == EmboarCast.Smog) travelVelocity = direction * (EmboarMoveRules.Range(Kind) / 0.3f);
        // Move clips only. Basic attacks deliberately have no animation integration.
        Play(Kind == EmboarCast.FlameCharge ? "Movement_run" : Kind == EmboarCast.HeatCrash || Kind == EmboarCast.Unite ? "Fight_attack_2" : Kind == EmboarCast.FirePledge ? "Fight_release" : "Fight_no_touch_attack");
        if (CanBrake) { ownSlot?.Dispose(); ownSlot = null; }
        Cooldown = EmboarMoveRules.Cooldown(Kind, IsUpgraded);
        wasMoveSuccessful = true;
        base.Finish(); // Payment is made once, at accepted commitment, never at effect expiry.
    }
    private void Play(string clip)
    {
        Animator animator = playerManager.AnimationManager.Animator;
        if (animator != null && animator.HasState(0, Animator.StringToHash(clip))) playerManager.AnimationManager.PlayAnimation(clip);
    }
    public override void Update()
    {
        if (IsActive) { ReadAim(); return; }
        if (!pending && !executing) return;
        float previous = elapsed; elapsed += Time.deltaTime;
        if (playerManager.Pokemon.CurrentHp <= 0 || playerManager.Pokemon.HasAnyStatusEffect(EmboarMoveRules.Interrupts)) { ResetMove(); return; }
        if (pending) { if (elapsed >= 5) ResetMove(); return; }
        if (EmboarMoveRules.Driven(Kind) && !movementEnded)
        {
            if (playerManager.Pokemon.HasStatusEffect(StatusType.Immobilized)) { ResetMove(); return; }
            if (Kind == EmboarCast.FlameCharge)
            {
                Vector2 input = playerManager.PlayerControls.Movement.Move.ReadValue<Vector2>();
                Vector3 desired = new Vector3(input.x, 0, input.y);
                if (desired.sqrMagnitude > 0.01f) direction = Vector3.RotateTowards(direction, desired.normalized, 3.5f * Time.deltaTime, 0);
                Vector3 step = direction * EmboarMoveRules.ChargeSpeed(elapsed, empowered) * Mathf.Min(Time.deltaTime, Mathf.Max(0, EmboarMoveRules.ChargeDuration - previous));
                Vector3 actual = playerManager.PlayerMovement.MoveWithWalls(step);
                playerManager.transform.rotation = Quaternion.LookRotation(direction);
                if (elapsed >= EmboarMoveRules.ChargeDuration || (step.sqrMagnitude > 0.0001f && actual.sqrMagnitude < step.sqrMagnitude * 0.02f)) EndMovement();
            }
            else
            {
                float start = Kind == EmboarCast.Unite ? EmboarMoveRules.UniteWindup : 0;
                float end = start + (Kind == EmboarCast.Unite ? EmboarMoveRules.UniteDrive : Kind == EmboarCast.HeatCrash ? EmboarMoveRules.CrashLeap : 0.3f);
                float delta = Mathf.Max(0, Mathf.Min(elapsed, end) - Mathf.Max(previous, start));
                if (delta > 0) playerManager.PlayerMovement.MoveWithWalls(travelVelocity * delta);
                if (elapsed >= end) EndMovement();
            }
            if (!movementEnded && elapsed >= nextReport)
            {
                nextReport = elapsed + 0.05f;
                playerManager.MovesController.EmboarMovementRPC(cast, false, playerManager.transform.position);
            }
        }
        if (executing && elapsed >= recovery) Complete();
    }
    private void EndMovement()
    {
        if (movementEnded) return;
        movementEnded = true;
        playerManager.MovesController.EmboarMovementRPC(cast, true, playerManager.transform.position);
        if (Kind == EmboarCast.FlameCharge) recovery = elapsed + 0.08f;
        if (Kind == EmboarCast.HeatCrash || Kind == EmboarCast.Unite) Play("Fight_landing");
    }
    private void Acquire()
    {
        rooted = true; playerManager.PlayerMovement.AddMovementRestriction();
        for (int i = 0; i < 3; i++)
            if (i == EmboarMoveRules.Slot(Kind)) ownSlot = playerManager.MovesController.AcquireMoveStatus(i, ActionStatusType.Busy);
            else leases.Add(playerManager.MovesController.AcquireMoveStatus(i, ActionStatusType.Busy));
        leases.Add(playerManager.MovesController.BasicAttackStatus.AcquireStatus(ActionStatusType.Busy));
        leases.Add(playerManager.MovesController.BattleItemStatus.AcquireStatus(ActionStatusType.Busy));
        leases.Add(playerManager.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
    }
    private void Complete()
    {
        bool hadAction = rooted || pending || executing || ownSlot != null || leases.Count > 0;
        ownSlot?.Dispose(); ownSlot = null;
        foreach (IDisposable lease in leases) lease.Dispose(); leases.Clear();
        if (rooted && playerManager != null) playerManager.PlayerMovement.RemoveMovementRestriction();
        rooted = pending = executing = recast = false;
        if (hadAction && playerManager != null && playerManager.IsSpawned && playerManager.NetworkManager != null && playerManager.NetworkManager.IsListening && playerManager.Pokemon.CurrentHp > 0
            && !playerManager.Pokemon.HasAnyStatusEffect(EmboarMoveRules.Interrupts)) playerManager.AnimationManager.SetTrigger("Transition");
    }
    private void HideAim()
    {
        if (Aim.Instance == null) return;
        if (Kind == EmboarCast.Ember) Aim.Instance.HideAutoAim();
        else if (Kind == EmboarCast.HeatCrash || Kind == EmboarCast.Unite) Aim.Instance.HideCircleAreaIndicator();
        else if (Kind != EmboarCast.FirePledge) Aim.Instance.HideSkillshotAim();
    }
    public override void Cancel() { if (IsActive) HideAim(); recast = false; base.Cancel(); }
    public override void ResetMove()
    {
        Cancel();
        if ((pending || executing) && playerManager != null && playerManager.IsSpawned && playerManager.NetworkManager != null && playerManager.NetworkManager.IsListening)
            playerManager.MovesController.CancelEmboarCastRPC(cast);
        Complete();
    }
}
