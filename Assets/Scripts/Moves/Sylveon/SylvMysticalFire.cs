public class SylvMysticalFire : PrefabMove
{
    // Preserve the existing serialized enum value/name.
    public override AvailableMoves MoveId => AvailableMoves.SylvMysticalCringe;
    public override string PrefabPath => SylvMoveRules.Folder + "SylvMysticalFire.prefab";
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => 3;
    public SylvMysticalFire() { Name = "Mystical Fire"; Cooldown = 7; }
    protected override void OnCommitted()
    {
        playerManager.PlayerMovement.StartDash(direction, Range, 0.25f);
        playerManager.AnimationManager.PlayAnimation("ani_spell1a_bat_0700");
    }
}
