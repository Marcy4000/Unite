public class JoltElectroweb : CombatMove
{
    public override AvailableMoves MoveId => AvailableMoves.JoltElectroWeb;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => JoltMoveRules.WebRange;
    public override bool LockDuringLoad => true;
    public JoltElectroweb() { Name = "Electroweb"; Cooldown = 10f; }
}
