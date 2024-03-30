using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlazingBycicleKick : MoveBase
{
    private GameObject target;
    private DamageInfo damageInfo;

    public BlazingBycicleKick()
    {
        name = "Blazing Bycicle Kick";
        cooldown = 0f;
        damageInfo = new DamageInfo(null, 2.47f, 14, 670, DamageType.Physical);
    }

    public override void Start(MovesController controller)
    {
        base.Start(controller);
        damageInfo.attacker = controller.Pokemon;
        Aim.Instance.InitializeAutoAim(11, 90);
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
