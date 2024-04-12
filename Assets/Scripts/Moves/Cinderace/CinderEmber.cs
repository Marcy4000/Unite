using System.Collections;
using System.Collections.Generic;
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
        name = "Ember";
        cooldown = 6.0f;
        distance = 8f;
        angle = 60f;
        damageInfo = new DamageInfo(0, 1, 7, 135, DamageType.Physical);
    }

    public override void Start(MovesController controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        Aim.Instance.InitializeAutoAim(distance, angle);
    }

    public override void Update()
    {
        if (!isActive)
        {
            return;
        }
        target = Aim.Instance.SureHitAim();
    }

    public override void Finish()
    {
        if (target != null && isActive)
        {
            movesController.LaunchHomingProjectileRpc(target.GetComponent<NetworkObject>().NetworkObjectId, damageInfo);
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideAutoAim();
        Debug.Log("Finished ember!");
        base.Finish();
    }
}
