public class SylvCalmMind : PrefabMove
{
    public override AvailableMoves MoveId => AvailableMoves.SylvCalmMind;
    public override bool IsOffensive => false;
    public override string PrefabPath => SylvMoveRules.Folder + "SylvCalmMind.prefab";
    public SylvCalmMind() { Name = "Calm Mind"; Cooldown = 8; }
    protected override void OnCommitted() { playerManager.AnimationManager.PlayAnimation("ani_spell2b_bat_0700"); }
}
