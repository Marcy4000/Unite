public class SylvUnite : CombatMove
{
    public override AvailableMoves MoveId => AvailableMoves.SylveonUnite;
    public override MoveAimStyle AimStyle => MoveAimStyle.Circle;
    public override float Range => SylvMoveRules.UniteRange;
    public override bool LockDuringLoad => true;
    public override bool LockMovementDuringLoad => false;

    public SylvUnite()
    {
        Name = "Fairy Frolic";
        Cooldown = 0f;
    }

    public override void Start(PlayerManager controller)
    {
        controller?.MovesController?.EnsureSylveonCombatRuntime();
        base.Start(controller);
    }
}
