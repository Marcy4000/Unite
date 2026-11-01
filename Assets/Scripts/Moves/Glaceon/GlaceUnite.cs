public class GlaceUnite : CombatMove
{
    public GlaceUnite() { Name = "Glacial Stage"; Cooldown = 0f; }
    public override AvailableMoves MoveId => AvailableMoves.GlaceonUnite;
    public override string PrefabPath => null;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => 6.2f;
}
