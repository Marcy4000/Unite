using UnityEngine;

public abstract class FlareonMoveBase : CombatMove
{
    private bool aimingRecast;
    public abstract FlareonCast Kind { get; }
    public override AvailableMoves MoveId => FlareonMoveRules.Move(Kind);
    public override bool LockDuringLoad => true;
    public override bool UsesExplicitUniteCastCheckpoint => Kind == FlareonCast.Unite;
    public override float Range => FlareonMoveRules.Range(aimingRecast ? FlareonCast.Slam : Kind);
    public override float TargetRangeTolerance => 0.4f;
    public override MoveAimStyle AimStyle => FlareonMoveRules.Targeted(Kind) ? MoveAimStyle.Target
        : Kind == FlareonCast.Swift || Kind == FlareonCast.HeatWave ? MoveAimStyle.Circle
        : FlareonMoveRules.Driven(Kind) ? MoveAimStyle.Dash : MoveAimStyle.Direction;
    public Vector3 AimDirection => FlareonMoveRules.Direction(direction, playerManager != null ? playerManager.transform.forward : Vector3.forward);

    public override void Start(PlayerManager controller)
    {
        aimingRecast = Kind == FlareonCast.Takedown && controller?.MovesController?.Combat?.IsFollowUpReady(0) == true;
        base.Start(controller);
    }

    public override void Finish()
    {
        if (!aimingRecast && !(Kind == FlareonCast.Takedown && playerManager?.MovesController?.Combat?.IsFollowUpReady(0) == true))
        { base.Finish(); return; }
        if (!IsActive || playerManager == null) return;
        aimingRecast = true;
        Pokemon victim = playerManager.MovesController.Combat?.GetFollowUpTarget(0);
        Vector3 aim = FlareonMoveRules.Direction(victim != null ? victim.transform.position - playerManager.transform.position : Vector3.zero, playerManager.transform.forward);
        bool valid = victim != null && IsTargetInCastRange(playerManager.transform.position, victim.transform.position);
        HideAim(!valid);
        IsActive = false;
        aimingRecast = false;
        if (valid) playerManager.MovesController.Combat?.Submit(this, aim, victim);
    }

    public override void Cancel()
    {
        aimingRecast = false;
        base.Cancel();
    }
}
