public class GlaceSwift : GlaceonMove
{
    public override AvailableMoves MoveId => AvailableMoves.GlaceSwift;
    public override MoveAimStyle AimStyle => MoveAimStyle.None;
    public override float Range => 0;
    public GlaceSwift() { Name = "Swift"; Cooldown = GlaceonMoveRules.Cooldown(MoveId); }
}
