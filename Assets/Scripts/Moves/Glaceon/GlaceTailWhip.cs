public class GlaceTailWhip : CombatMove
{
    public GlaceTailWhip() { Name = "Tail Whip"; Cooldown = 8f; }
    public override AvailableMoves MoveId => AvailableMoves.GlaceTailWhip;
    public override string PrefabPath => null;
    public override MoveAimStyle AimStyle => MoveAimStyle.Target;
    public override float Range => 8f;
}
