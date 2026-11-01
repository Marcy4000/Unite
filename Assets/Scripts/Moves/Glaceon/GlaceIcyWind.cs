public class GlaceIcyWind : CombatMove
{
    public GlaceIcyWind() { Name = "Icy Wind"; Cooldown = 6.5f; }
    public override AvailableMoves MoveId => AvailableMoves.GlaceIcyWind;
    public override string PrefabPath => null;
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => 4f;
    public override bool IsOffensive => false;
}
