public class GlaceFreezeDry : CombatMove
{
    public GlaceFreezeDry() { Name = "Freeze Dry"; Cooldown = 8.5f; }
    public override AvailableMoves MoveId => AvailableMoves.GlaceFreezeDry;
    public override string PrefabPath => null;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => 6f;
}
