public class GlaceIceShard : CombatMove
{
    public GlaceIceShard() { Name = "Ice Shard"; Cooldown = 8.5f; }
    public override AvailableMoves MoveId => AvailableMoves.GlaceIceShard;
    public override string PrefabPath => null;
    public override MoveAimStyle AimStyle => MoveAimStyle.None;
    public override float Range => 0f;
    public override bool IsOffensive => false;
}
