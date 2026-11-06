public class GlaceFreezeDry : GlaceonMove
{
    public override AvailableMoves MoveId => AvailableMoves.GlaceFreezeDry;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => 6;
    public override string PrefabPath => GlaceonMoveRules.Folder + "FreezeDryProjectile.prefab";
    public GlaceFreezeDry() { Name = "Freeze Dry"; Cooldown = GlaceonMoveRules.Cooldown(MoveId); }
}
