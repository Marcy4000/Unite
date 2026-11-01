public class SylvDrainingKiss : CombatMove
{
    public override AvailableMoves MoveId => AvailableMoves.SylvDrainingKiss;
    public override MoveAimStyle AimStyle => MoveAimStyle.Target;
    public override float Range => SylvMoveRules.KissRange;
    public override bool LockDuringLoad => true;

    public SylvDrainingKiss()
    {
        Name = "Draining Kiss";
        Cooldown = SylvMoveRules.KissCooldown;
    }

    public override void Start(PlayerManager controller)
    {
        controller?.MovesController?.EnsureSylveonCombatRuntime();
        base.Start(controller);
    }
}
