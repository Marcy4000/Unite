using UnityEngine;

public abstract class UmbreonMove : PrefabMove
{
    private int request;
    public override int ExecutionRequest => request;
    protected virtual bool ReusesRequest => false;
    public override void Start(PlayerManager controller)
    {
        if (IsActive) return;
        if (!ReusesRequest) request++;
        base.Start(controller);
    }
    public override void ResetMove() { base.ResetMove(); request++; }
    public override string PrefabPath => UmbreonMoveRules.Folder + MoveId + ".prefab";
    public override bool RequiresServerAcceptance => true;
    public override bool LockDuringLoad => true;
}
public class UmbreonSwift : UmbreonMove
{
    public override AvailableMoves MoveId => AvailableMoves.UmbreonSwift;
    public override MoveAimStyle AimStyle => MoveAimStyle.Target;
    public override float Range => UmbreonMoveRules.SwiftRange;
    public UmbreonSwift() { Name = "Swift"; Cooldown = 6; }
}
public class UmbreonFakeTears : UmbreonMove
{
    public override AvailableMoves MoveId => AvailableMoves.UmbreonFakeTears;
    public override MoveAimStyle AimStyle => MoveAimStyle.Circle;
    public override float Range => UmbreonMoveRules.TearsRadius;
    public UmbreonFakeTears() { Name = "Fake Tears"; Cooldown = 8; }
}
public class UmbreonMeanLook : UmbreonMove
{
    public override AvailableMoves MoveId => AvailableMoves.UmbreonMeanLook;
    public override MoveAimStyle AimStyle => MoveAimStyle.Target;
    public override float Range => UmbreonMoveRules.MeanRange;
    public UmbreonMeanLook() { Name = "Mean Look"; Cooldown = 9; }
}
public class UmbreonFoulPlay : UmbreonMove
{
    private bool second;
    private Sprite secondIcon;
    protected override bool ReusesRequest => second;
    public override AvailableMoves MoveId => AvailableMoves.UmbreonFoulPlay;
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => second ? UmbreonMoveRules.SecondDashDistance : UmbreonMoveRules.DashDistance;
    public override bool CooldownOnEffectEnd => true;
    public override Sprite IconOverride => second ? secondIcon != null ? secondIcon : secondIcon = Resources.Load<Sprite>(UmbreonMoveRules.Visuals + "FoulFollowup") : null;
    protected override void OnExecutionState(bool finished) { second = !finished; }
    public UmbreonFoulPlay() { Name = "Foul Play"; Cooldown = 7.5f; }
}
public class UmbreonWish : UmbreonMove
{
    public override AvailableMoves MoveId => AvailableMoves.UmbreonWish;
    public override MoveAimStyle AimStyle => MoveAimStyle.Target;
    public override AimTarget TargetTeam => AimTarget.Ally;
    public override bool AllowSelfTarget => true;
    public override bool IsOffensive => false;
    public override float Range => UmbreonMoveRules.WishRange;
    public UmbreonWish() { Name = "Wish"; Cooldown = 6; }
}
public class UmbreonSnarl : UmbreonMove
{
    public override AvailableMoves MoveId => AvailableMoves.UmbreonSnarl;
    public override MoveAimStyle AimStyle => MoveAimStyle.Circle;
    public override float Range => UmbreonMoveRules.SnarlRadius;
    public UmbreonSnarl() { Name = "Snarl"; Cooldown = 10; }
}
public class UmbreonMoonlightPrance : UmbreonMove
{
    public override AvailableMoves MoveId => AvailableMoves.UmbreonMoonlightPrance;
    public override MoveAimStyle AimStyle => MoveAimStyle.Circle;
    public override float Range => UmbreonMoveRules.UniteAuraRadius;
    public UmbreonMoonlightPrance() { Name = "Moonlight Prance"; Cooldown = 0; }
}
