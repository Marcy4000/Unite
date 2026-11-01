public class JolteonUnite : CombatMove
{
    public override AvailableMoves MoveId => AvailableMoves.JolteonUnite;
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => 5f;
    public override bool LockDuringLoad => true;
    public override bool UsesExplicitUniteCastCheckpoint => true;
    public JolteonUnite() { Name = "Lightning Blitz"; Cooldown = 0f; }
}
