using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class CinderFlameCharge : MoveBase
{
    private Vector3 direction;
    private DamageInfo damageInfo;

    public CinderFlameCharge()
    {
        name = "Flame Charge";
        cooldown = 5.0f;
        damageInfo = new DamageInfo(0, 0.47f, 3, 130, DamageType.Physical);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeDashAim(5f);
        Debug.Log($"Executed {name}!");
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
    }

    public override void Update()
    {
        if (!isActive)
        {
            return;
        }
        direction = Aim.Instance.DashAim();
    }

    public override void Finish()
    {
        playerManager.PlayerMovement.StartDash(direction);
        playerManager.AnimationManager.PlayAnimation($"ani_spell2a_bat_0815");
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        wasMoveSuccessful = true;
        Aim.Instance.HideDashAim();
        base.Finish();
    }
}
