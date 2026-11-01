public class SylvMysticalFire : CombatMove
{
    // Preserve the existing serialized enum value/name.
    public override AvailableMoves MoveId => AvailableMoves.SylvMysticalCringe;
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => SylvMoveRules.FireDashRange;
    public override bool LockDuringLoad => true;

    public SylvMysticalFire()
    {
        Name = "Mystical Fire";
        Cooldown = SylvMoveRules.FireCooldown;
    }

    public override void Start(PlayerManager controller)
    {
        controller?.MovesController?.EnsureSylveonCombatRuntime();
        base.Start(controller);
    }
}
