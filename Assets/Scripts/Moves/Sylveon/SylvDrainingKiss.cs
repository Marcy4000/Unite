public class SylvDrainingKiss : PrefabMove
{
    public override AvailableMoves MoveId => AvailableMoves.SylvDrainingKiss;
    public override string PrefabPath => SylvMoveRules.Folder + "SylvDrainingKiss.prefab";
    public override MoveAimStyle AimStyle => MoveAimStyle.Target;
    public override float Range => SylvMoveRules.KissRange;
    public SylvDrainingKiss() { Name = "Draining Kiss"; Cooldown = 9; }
    protected override void OnCommitted() { playerManager.AnimationManager.PlayAnimation("ani_spell2_bat_0700"); }
}
