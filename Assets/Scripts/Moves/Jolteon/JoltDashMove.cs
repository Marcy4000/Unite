public abstract class JoltDashMove : CombatMove
{
    protected abstract bool IsFang { get; }
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => IsFang ? JoltMoveRules.FangRange : JoltMoveRules.BoostedDischargeRange;
    public override bool LockDuringLoad => true;
    public override float IndicatorRange
    {
        get
        {
            if (IsFang) return JoltMoveRules.FangRange;
            var runtime = playerManager?.MovesController?.Combat?.Character as JolteonCharacterRuntime;
            bool recast = runtime != null && runtime.RecastAvailable && runtime.RecastMove == MoveId;
            bool empowered = recast ? runtime.RecastEmpowered : runtime?.PassiveReady == true;
            float distance = empowered ? JoltMoveRules.BoostedDischargeRange : JoltMoveRules.DischargeRange;
            return recast ? distance * JoltMoveRules.RecastScale : distance;
        }
    }
}
