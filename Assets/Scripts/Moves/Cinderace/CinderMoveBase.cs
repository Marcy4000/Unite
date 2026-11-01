public abstract class CinderMoveBase : CombatMove
{
    public abstract CinderCast Kind { get; }
    public override AvailableMoves MoveId => CinderMoveRules.Move(Kind);
    public override string PrefabPath => null;
    public override MoveAimStyle AimStyle => CinderMoveRules.Targeted(Kind) ? MoveAimStyle.Target
        : CinderMoveRules.Dash(Kind) ? MoveAimStyle.Dash
        : Kind == CinderCast.PyroBall ? MoveAimStyle.Direction
        : MoveAimStyle.None;
    public override float Range => Kind == CinderCast.Feint ? 0f : CinderMoveRules.Range(Kind);
    public override bool IsOffensive => Kind != CinderCast.Feint;
    public override bool LockDuringLoad => Kind != CinderCast.Feint;

    protected CinderMoveBase()
    {
        Cooldown = CinderMoveRules.Cooldown(Kind);
    }
}
