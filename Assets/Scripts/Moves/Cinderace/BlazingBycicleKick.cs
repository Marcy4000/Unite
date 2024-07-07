using Unity.Netcode;
using UnityEngine;

public class BlazingBycicleKick : MoveBase
{
    private GameObject target;
    private DamageInfo damageInfo;

    public BlazingBycicleKick()
    {
        Name = "Blazing Bycicle Kick";
        Cooldown = 0f;
        damageInfo = new DamageInfo(0, 2.47f, 14, 670, DamageType.Physical);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        Aim.Instance.InitializeAutoAim(11, 90, AimTarget.NonAlly);
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }
        target = Aim.Instance.SureHitAim();
    }

    public override void Finish()
    {
        if (target != null && IsActive)
        {
            playerManager.MovesController.LaunchHomingProjectileRpc(target.GetComponent<NetworkObject>().NetworkObjectId, damageInfo);
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideAutoAim();
        Debug.Log("Finished ember!");
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideAutoAim();
    }
}
