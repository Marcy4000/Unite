using UnityEngine;

public class SylvSwift : CombatMove
{
    public override AvailableMoves MoveId => AvailableMoves.SylvSwift;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => SylvMoveRules.SwiftRange;
    public override bool LockDuringLoad => true;
    public override bool LockMovementDuringLoad => false;

    public SylvSwift()
    {
        Name = "Swift";
        Cooldown = SylvMoveRules.SwiftCooldown;
    }

    public override void Start(PlayerManager controller)
    {
        controller?.MovesController?.EnsureSylveonCombatRuntime();
        base.Start(controller);
    }
}
