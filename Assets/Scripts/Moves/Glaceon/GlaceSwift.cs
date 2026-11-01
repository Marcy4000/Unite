public class GlaceSwift : CombatMove
{
    public GlaceSwift() { Name = "Swift"; Cooldown = 5f; }
    public override AvailableMoves MoveId => AvailableMoves.GlaceSwift;
    public override string PrefabPath => null;
    public override MoveAimStyle AimStyle => MoveAimStyle.None;
    public override float Range => 0f;
    public override bool IsOffensive => false;
}
