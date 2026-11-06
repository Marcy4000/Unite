public class GlaceIcyWind : GlaceonMove
{
    public override AvailableMoves MoveId => AvailableMoves.GlaceIcyWind;
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => 4;
    public GlaceIcyWind() { Name = "Icy Wind"; Cooldown = GlaceonMoveRules.Cooldown(MoveId); }
}
