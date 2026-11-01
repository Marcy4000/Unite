public class JoltAgility : CombatMove
{
    public override AvailableMoves MoveId => AvailableMoves.JoltAgility;
    public override MoveAimStyle AimStyle => MoveAimStyle.None;
    public override bool IsOffensive => false;
    public JoltAgility() { Name = "Agility"; Cooldown = 7f; }
}
