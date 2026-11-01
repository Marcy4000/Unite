public class SylvBabyDollEyes : CombatMove
{
    public override AvailableMoves MoveId => AvailableMoves.SylvBabyDollEyes;
    public override MoveAimStyle AimStyle => MoveAimStyle.Target;
    public override float Range => SylvMoveRules.BabyDollEyesRange;
    public override bool LockDuringLoad => true;

    public SylvBabyDollEyes()
    {
        Name = "Baby-Doll Eyes";
        Cooldown = SylvMoveRules.BabyDollEyesCooldown;
    }

    public override void Start(PlayerManager controller)
    {
        controller?.MovesController?.EnsureSylveonCombatRuntime();
        base.Start(controller);
    }
}
