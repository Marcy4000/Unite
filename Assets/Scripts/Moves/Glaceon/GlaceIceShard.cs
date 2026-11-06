public class GlaceIceShard : GlaceonMove
{
    public override AvailableMoves MoveId => AvailableMoves.GlaceIceShard;
    public override MoveAimStyle AimStyle => MoveAimStyle.None;
    public override float Range => 0;
    public GlaceIceShard() { Name = "Ice Shard"; Cooldown = GlaceonMoveRules.Cooldown(MoveId); }
}
