using Unity.Netcode;
using UnityEngine;

public class CinderEmber : MoveBase
{
    private GameObject target;
    private DamageInfo damageInfo;
    private float distance;
    private float angle;

    public CinderEmber()
    {
        Name = "Ember";
        Cooldown = 6.0f;
        distance = 8f;
        angle = 60f;
        damageInfo = new DamageInfo(0, 1, 7, 135, DamageType.Physical);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        Aim.Instance.InitializeAutoAim(distance, angle, AimTarget.NonAlly);
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
            playerManager.AnimationManager.PlayAnimation($"ani_spell1_bat_0815");
            playerManager.StopMovementForTime(0.4f);
            playerManager.transform.LookAt(target.transform);
            playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);
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

    public override void ResetMove()
    {
        target = null;
    }
}
