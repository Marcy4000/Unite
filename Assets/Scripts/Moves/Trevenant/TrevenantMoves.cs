using UnityEngine;

public abstract class TrevenantMove : PrefabMove
{
    private int request;
    private bool recasting;
    public override int ExecutionRequest => request;
    public override string PrefabPath => TrevenantMoveRules.Folder + MoveId + ".prefab";
    public override bool RequiresServerAcceptance => true;
    public override bool LockDuringLoad => true;
    public override bool CooldownOnEffectEnd => true;
    public override void Start(PlayerManager controller) { if (IsActive) return; if (!recasting) request++; base.Start(controller); }
    protected override void OnExecutionState(bool finished) { recasting = !finished; }
    public void CompleteExecution(float remaining)
    {
        ReceiveExecutionState(true);
        if (playerManager != null) playerManager.MovesController.SetTrevenantOwnerCooldown(this, remaining);
    }
    public override void ResetMove() { base.ResetMove(); request++; recasting = false; }
}
public class TrevenantWillOWisp : TrevenantMove
{
    public override AvailableMoves MoveId => AvailableMoves.TrevenantWillOWisp;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => TrevenantMoveRules.WispRange;
    public TrevenantWillOWisp() { Name = "Will-o-Wisp"; Cooldown = 6; }
}
public class TrevenantBranchPoke : TrevenantMove
{
    public override AvailableMoves MoveId => AvailableMoves.TrevenantBranchPoke;
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => TrevenantMoveRules.BranchRange;
    public TrevenantBranchPoke() { Name = "Branch Poke"; Cooldown = 7.5f; }
}
public class TrevenantWoodHammer : TrevenantMove
{
    private bool second;
    public override AvailableMoves MoveId => AvailableMoves.TrevenantWoodHammer;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => second ? TrevenantMoveRules.HammerSecondRange : TrevenantMoveRules.HammerRange;
    public override string IndicatorPath => TrevenantMoveRules.Visuals + (second ? "HammerLineIndicator" : "pref_Indicator_Sector_180_lv3");
    public override Vector3 IndicatorScale => second
        ? new Vector3(TrevenantMoveRules.HammerSecondWidth / TrevenantMoveRules.AuthoredHammerWidth, 1, TrevenantMoveRules.HammerSecondRange / TrevenantMoveRules.AuthoredHammerLength)
        : new Vector3(Range / TrevenantMoveRules.AuthoredHammerRange, 1, Range / TrevenantMoveRules.AuthoredHammerRange);
    public override Sprite IconOverride => second ? Resources.Load<Sprite>(TrevenantMoveRules.Visuals + "HammerFollowup") : null;
    protected override void OnExecutionState(bool finished) { base.OnExecutionState(finished); second = !finished; }
    public TrevenantWoodHammer() { Name = "Wood Hammer"; Cooldown = 8; }
}
public class TrevenantCurse : TrevenantMove
{
    private bool detonating;
    public override AvailableMoves MoveId => AvailableMoves.TrevenantCurse;
    public override MoveAimStyle AimStyle => MoveAimStyle.Circle;
    public override float Range => TrevenantMoveRules.CurseArea(IsUpgraded);
    public override Sprite IconOverride => detonating ? Resources.Load<Sprite>(TrevenantMoveRules.Visuals + "CurseDetonate") : null;
    protected override void OnExecutionState(bool finished) { base.OnExecutionState(finished); detonating = !finished; }
    public TrevenantCurse() { Name = "Curse"; Cooldown = 6; }
}
public class TrevenantHornLeech : TrevenantMove
{
    public override AvailableMoves MoveId => AvailableMoves.TrevenantHornLeech;
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => TrevenantMoveRules.HornRange;
    public TrevenantHornLeech() { Name = "Horn Leech"; Cooldown = 9; }
}
public class TrevenantPainSplit : TrevenantMove
{
    public override AvailableMoves MoveId => AvailableMoves.TrevenantPainSplit;
    public override MoveAimStyle AimStyle => MoveAimStyle.Target;
    public override float Range => TrevenantMoveRules.PainRange;
    public TrevenantPainSplit() { Name = "Pain Split"; Cooldown = 10; }
}
public class TrevenantPhantomForest : TrevenantMove
{
    public override AvailableMoves MoveId => AvailableMoves.TrevenantPhantomForest;
    public override MoveAimStyle AimStyle => MoveAimStyle.Ground;
    public override float Range => TrevenantMoveRules.UniteRange;
    public override float AreaRadius => TrevenantMoveRules.UniteRadius;
    public TrevenantPhantomForest() { Name = "Phantom Forest"; Cooldown = 0; }
}
