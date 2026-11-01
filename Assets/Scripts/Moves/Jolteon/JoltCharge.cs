public class JoltCharge : CombatMove
{
    public override AvailableMoves MoveId => AvailableMoves.JoltCharge;
    public override MoveAimStyle AimStyle => MoveAimStyle.None;
    public override bool IsOffensive => false;
    public JoltCharge() { Name = "Charge"; Cooldown = 9f; }
}
