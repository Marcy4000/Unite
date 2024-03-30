using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CinderEmber : MoveBase
{
    private GameObject target;
    private DamageInfo damageInfo;

    public CinderEmber()
    {
        name = "Ember";
        cooldown = 6.0f;
        damageInfo = new DamageInfo(null, 1, 7, 135, DamageType.Physical);
    }

    public override void Start(MovesController controller)
    {
        base.Start(controller);
        damageInfo.attacker = controller.Pokemon;
        Aim.Instance.InitializeAutoAim(8, 60);
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
            movesController.LaunchHomingProjectile(target.transform, damageInfo);
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideAutoAim();
        Debug.Log("Finished ember!");
        base.Finish();
    }
}
