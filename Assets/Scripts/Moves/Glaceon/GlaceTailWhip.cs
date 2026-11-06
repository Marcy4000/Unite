public class GlaceTailWhip : GlaceonMove
{
    public override AvailableMoves MoveId => AvailableMoves.GlaceTailWhip;
    public override MoveAimStyle AimStyle => MoveAimStyle.Target;
    public override float Range => 8;
    public GlaceTailWhip() { Name = "Tail Whip"; Cooldown = GlaceonMoveRules.Cooldown(MoveId); }
}
