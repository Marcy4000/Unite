public class JoltDischarge : JoltDashMove
{
    protected override bool IsFang => false;
    public JoltDischarge() { Name = "Discharge"; Cooldown = 11f; }
}
