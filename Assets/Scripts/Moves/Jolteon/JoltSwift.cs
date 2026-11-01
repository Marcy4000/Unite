public class JoltSwift : CombatMove
{
    public override AvailableMoves MoveId => AvailableMoves.JoltSwift;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => 5f;
    public override bool LockDuringLoad => true;
    public JoltSwift() { Name = "Swift"; Cooldown = 7f; }
}
