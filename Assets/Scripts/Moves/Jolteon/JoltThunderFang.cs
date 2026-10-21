public class JoltThunderFang : JoltDashMove
{
    protected override bool IsFang => true;
    public JoltThunderFang() { Name = "Thunder Fang"; Cooldown = 7f; }
}
