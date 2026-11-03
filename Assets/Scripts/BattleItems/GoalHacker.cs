public class GoalHacker : ServerBattleItem
{
    public GoalHacker() : base(AvailableBattleItems.GoalHacker) { }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        playerManager.Aim.InitializeCircleAreaIndicator(BattleItemRules.TargetRange);
    }

    public override void Update()
    {
        if (IsActive) playerManager.Aim.CircleAreaAim();
    }

    public override void Finish()
    {
        if (!IsActive || playerManager == null) return;
        var point = playerManager.Aim.CircleAreaAim();
        playerManager.Aim.HideCircleAreaIndicator();
        Submit(point - playerManager.transform.position);
    }

    public override void Cancel()
    {
        if (IsActive && playerManager != null && playerManager.Aim != null) playerManager.Aim.HideCircleAreaIndicator();
        base.Cancel();
    }
}
