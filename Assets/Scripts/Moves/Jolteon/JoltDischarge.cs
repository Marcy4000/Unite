public class JoltDischarge : JoltDashMove
{
    protected override bool IsFang => false;
    public override AvailableMoves MoveId => AvailableMoves.JoltDischarge;
    public JoltDischarge() { Name = "Discharge"; Cooldown = 11f; }
}
