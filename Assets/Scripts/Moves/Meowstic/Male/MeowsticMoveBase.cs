using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class MeowsticMoveBase : MoveBase
{
    public abstract MeowsticCast Kind { get; }
    private readonly List<IDisposable> locks = new List<IDisposable>();
    private Vector3 direction, destination;
    private Pokemon target;
    private float recoveryUntil;
    private bool executing, rooted, recast;
    public bool IsExecuting => executing;
    public float RecoveryRemaining => Mathf.Max(0, recoveryUntil - Time.time);
    protected MeowsticMoveBase()
    {
        Name = Kind == MeowsticCast.MagicCoat ? "Magic Coat" : Kind == MeowsticCast.WonderRoom ? "Wonder Room" : Kind == MeowsticCast.Unite ? "Mystic Harmony" : Kind.ToString();
        Cooldown = Kind == MeowsticCast.Unite ? 0 : MeowsticMoveRules.Cooldown(Kind);
    }
    public override void Start(PlayerManager controller)
    {
        if (executing) return;
        base.Start(controller);
        recast = Kind == MeowsticCast.Reflect && controller.MovesController.MeowsticReflectAvailable;
        if (recast) return;
        if (MeowsticMoveRules.Support(Kind)) ActorAim.InitializeAutoAim(MeowsticMoveRules.Range(Kind), 60, AimTarget.Ally);
        else if (Kind == MeowsticCast.WonderRoom)
        {
            ActorAim.InitializeSquareAreaIndicator(MeowsticMoveRules.Range(Kind));
            controller.MovesController.ShowMeowsticRoomAim(true, controller.transform.position);
        }
        else if (Kind == MeowsticCast.Scratch || Kind == MeowsticCast.Leer) ActorAim.InitializeSkillshotAim(MeowsticMoveRules.Range(Kind));
        else ActorAim.InitializeSimpleCircle(MeowsticMoveRules.Range(Kind));
        ReadAim();
    }
    private void ReadAim()
    {
        if (recast) return;
        if (MeowsticMoveRules.Support(Kind))
        {
            GameObject aim = ActorAim.SureHitAim();
            target = aim != null ? aim.GetComponent<Pokemon>() : null;
            direction = target != null ? target.transform.position - playerManager.transform.position : playerManager.transform.forward;
        }
        else if (Kind == MeowsticCast.WonderRoom)
        {
            destination = ActorAim.CircleAreaAim();
            direction = playerManager.transform.position - destination;
            playerManager.MovesController.ShowMeowsticRoomAim(true, destination);
        }
        else if (Kind == MeowsticCast.Scratch || Kind == MeowsticCast.Leer) direction = ActorAim.SkillshotAim();
        else direction = playerManager.transform.forward;
        direction = MeowsticMoveRules.Direction(direction, playerManager.transform.forward);
    }
    public override void Finish()
    {
        if (!IsActive || executing) return;
        ReadAim(); HideAim();
        if (recast)
        {
            playerManager.MovesController.DetonateMeowsticReflectRPC();
            base.Finish(); return; // No second energy/cooldown spend.
        }
        executing = true; recoveryUntil = Time.time + MeowsticMoveRules.Commitment(Kind);
        playerManager.MovesController.EndMeowsticBasicRecovery(false);
        for (int slot = 0; slot < 3; slot++) locks.Add(playerManager.MovesController.AcquireMoveStatus(slot, ActionStatusType.Disabled));
        locks.Add(playerManager.MovesController.BasicAttackStatus.AcquireStatus(ActionStatusType.Disabled));
        locks.Add(playerManager.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
        rooted = true; playerManager.PlayerMovement.AddMovementRestriction();
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        string species = playerManager.Pokemon.CurrentLevel >= 3 ? "0734" : "0733";
        playerManager.AnimationManager.PlayAnimation("Armature_pm" + species + "_00_" + (Kind == MeowsticCast.Scratch || Kind == MeowsticCast.Reflect || Kind == MeowsticCast.Psychic ? "ba20_buturi01" : "ba21_tokusyu01"));
        playerManager.MovesController.CastMeowsticRPC(Kind, direction, destination, target != null ? target.NetworkObjectId : ulong.MaxValue);
        wasMoveSuccessful = true; base.Finish();
    }
    public override void Update()
    {
        if (IsActive) ReadAim();
        if (executing && (Time.time >= recoveryUntil || playerManager.Pokemon.CurrentHp <= 0 || playerManager.Pokemon.HasAnyStatusEffect(MeowsticMoveRules.Interrupts))) Complete();
    }
    private void Complete()
    {
        if (!executing) return;
        executing = false;
        foreach (IDisposable lease in locks) lease.Dispose(); locks.Clear();
        if (rooted && playerManager != null) playerManager.PlayerMovement.RemoveMovementRestriction();
        rooted = false;
        if (playerManager != null && playerManager.Pokemon.CurrentHp > 0 && !playerManager.Pokemon.HasAnyStatusEffect(MeowsticMoveRules.Interrupts)) playerManager.AnimationManager.SetTrigger("Transition");
    }
    private void HideAim()
    {
        if (ActorAim == null || recast) return;
        if (MeowsticMoveRules.Support(Kind)) ActorAim.HideAutoAim();
        else if (Kind == MeowsticCast.WonderRoom) { ActorAim.HideCircleAreaIndicator(); playerManager.MovesController.ShowMeowsticRoomAim(false, destination); }
        else if (Kind == MeowsticCast.Scratch || Kind == MeowsticCast.Leer) ActorAim.HideSkillshotAim();
        else ActorAim.HideSimpleCircle();
    }
    public override void Cancel() { if (IsActive) HideAim(); base.Cancel(); }
    public override void ResetMove() { Cancel(); Complete(); target = null; }
}
