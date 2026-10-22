public class SylvBabyDollEyes : PrefabMove
{
    public override AvailableMoves MoveId => AvailableMoves.SylvBabyDollEyes;
    public override string PrefabPath => SylvMoveRules.Folder + "SylvBabyDollEyes.prefab";
    public override MoveAimStyle AimStyle => MoveAimStyle.Target;
    public override float Range => 6;
    public SylvBabyDollEyes() { Name = "Baby-Doll Eyes"; Cooldown = 5; }
    protected override void OnCommitted()
    {
        playerManager.StopMovementForTime(0.25f);
        playerManager.AnimationManager.PlayAnimation("ani_spell2_bat_0133");
    }
}
