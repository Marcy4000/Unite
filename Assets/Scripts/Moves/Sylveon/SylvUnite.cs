public class SylvUnite : PrefabMove
{
    public override AvailableMoves MoveId => AvailableMoves.SylveonUnite;
    public override string PrefabPath => SylvMoveRules.Folder + "SylvFairyFrolic.prefab";
    public override MoveAimStyle AimStyle => MoveAimStyle.Circle;
    public override float Range => 5;
    public SylvUnite() { Name = "Fairy Frolic"; Cooldown = 0; }
}
