using UnityEngine;

public class BrickLol : ServerBattleItem
{
    public BrickLol() : base(AvailableBattleItems.Brick) { }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        playerManager.Aim.InitializeCircleAreaIndicator(5);
    }

    public override void Update()
    {
        if (IsActive) playerManager.Aim.CircleAreaAim();
    }

    public override void Finish()
    {
        if (!IsActive || playerManager == null) return;
        Vector3 destination = playerManager.Aim.CircleAreaAim();
        playerManager.Aim.HideCircleAreaIndicator();
        Submit(destination - playerManager.transform.position);
    }

    public override void Cancel()
    {
        if (IsActive && playerManager != null && playerManager.Aim != null) playerManager.Aim.HideCircleAreaIndicator();
        base.Cancel();
    }
}
