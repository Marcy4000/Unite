using UnityEngine;

public class EjectButton : ServerBattleItem
{
    private Vector3 dashDirection;

    public EjectButton() : base(AvailableBattleItems.Eject) { }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        dashDirection = Vector3.zero;
        playerManager.Aim.InitializeDashAim(BattleItemRules.EjectRange);
    }

    public override void Update()
    {
        if (IsActive) dashDirection = playerManager.Aim.DashAim();
    }

    public override void Finish()
    {
        if (!IsActive || playerManager == null) return;
        dashDirection = playerManager.Aim.DashAim();
        playerManager.Aim.HideDashAim();
        Submit(dashDirection);
    }

    public override void Cancel()
    {
        if (IsActive && playerManager != null && playerManager.Aim != null) playerManager.Aim.HideDashAim();
        dashDirection = Vector3.zero;
        base.Cancel();
    }
}
