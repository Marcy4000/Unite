using UnityEngine;

public class EjectButton : BattleItemBase
{
    private float distance = 5f;
    private Vector3 dashDirection;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        wasUseSuccessful = false;

        Aim.Instance.InitializeDashAim(distance);
        Cooldown = 55f;
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }

        dashDirection = Aim.Instance.DashAim();
    }

    public override void Finish()
    {
        if (IsActive && dashDirection.magnitude != 0)
        {
            wasUseSuccessful = true;
            Vector3 newPosition = playerManager.transform.position + (dashDirection.normalized * distance);
            playerManager.UpdatePosAndRotRPC(newPosition, Quaternion.LookRotation(dashDirection));
        }
        Aim.Instance.HideDashAim();
        base.Finish();
    }

    public override void Cancel()
    {
        Aim.Instance.HideDashAim();
        base.Cancel();
    }
}
