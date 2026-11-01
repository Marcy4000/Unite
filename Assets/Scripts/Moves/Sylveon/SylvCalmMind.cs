public class SylvCalmMind : CombatMove
{
    public override AvailableMoves MoveId => AvailableMoves.SylvCalmMind;
    public override MoveAimStyle AimStyle => MoveAimStyle.None;
    public override bool IsOffensive => false;

    public SylvCalmMind()
    {
        Name = "Calm Mind";
        Cooldown = SylvMoveRules.CalmCooldown;
    }

    public override void Start(PlayerManager controller)
    {
        controller?.MovesController?.EnsureSylveonCombatRuntime();
        base.Start(controller);
    }
}
