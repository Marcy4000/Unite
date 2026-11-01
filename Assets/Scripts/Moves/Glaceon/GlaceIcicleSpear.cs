public class GlaceIcicleSpear : CombatMove
{
    public GlaceIcicleSpear() { Name = "Icicle Spear"; Cooldown = 5f; }
    public override AvailableMoves MoveId => AvailableMoves.GlaceIcycleSpear;
    public override string PrefabPath => null;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => 9f;
    public override bool StartsOnPress => true;
    public override bool EndsOnRelease => false;
    public override bool CooldownOnEffectEnd => true;
}
