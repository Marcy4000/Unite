using UnityEngine;

public class EjectButton : BattleItemBase
{
    private float distance = 5f;
    private Vector3 dashDirection;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        wasUseSuccessful = false;

        playerManager.Aim.InitializeDashAim(distance);
        Cooldown = 55f;
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }

        dashDirection = playerManager.Aim.DashAim();
    }

    public override void Finish()
    {
        if (!IsActive || playerManager == null) return;
        if (playerManager.IsBot) dashDirection = playerManager.Aim.DashAim();
        if (IsActive && dashDirection.magnitude != 0)
        {
            wasUseSuccessful = true;
            Vector3 newPosition = playerManager.transform.position + (dashDirection.normalized * distance);
            playerManager.UpdatePosAndRotRPC(newPosition, Quaternion.LookRotation(dashDirection));
            SpatialAudioManager.Instance.PlaySpatialSound(DefaultAudioSounds.Play_Props_EjectButton_ShanXian, playerManager.Vision, AudioAudibility.Everyone, playerManager);
        }
        playerManager.Aim.HideDashAim();
        base.Finish();
    }

    public override void Cancel()
    {
        if (IsActive && playerManager != null && playerManager.Aim != null) playerManager.Aim.HideDashAim();
        base.Cancel();
    }
}
