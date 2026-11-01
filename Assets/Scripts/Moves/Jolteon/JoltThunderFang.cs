public class JoltThunderFang : JoltDashMove
{
    protected override bool IsFang => true;
    public override AvailableMoves MoveId => AvailableMoves.JoltThunderFang;
    public JoltThunderFang() { Name = "Thunder Fang"; Cooldown = 7f; }
}
