using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GlaceTailWhip : MoveBase
{
    private GameObject target;
    private DamageInfo damageInfo;
    private float distance;
    private float angle;

    private string attackPrefab;

    public GlaceTailWhip()
    {
        Name = "Tail Whip";
        Cooldown = 8.0f;
        distance = 8f;
        angle = 60f;
        damageInfo = new DamageInfo(0, 0.61f, 6, 190, DamageType.Special);
        attackPrefab = "Moves/Glaceon/GlaceonTailWhip";
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        Aim.Instance.InitializeAutoAim(distance, angle);
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
            playerManager.MovesController.LaunchProjectileFromPath(target.GetComponent<NetworkObject>().NetworkObjectId, damageInfo, attackPrefab);
            string animation = playerManager.Pokemon.CurrentLevel.Value >= 3 ? "ani_spell2_bat_0471" : "ani_spell2G_bat_0133";
            playerManager.AnimationManager.PlayAnimation(animation);
            playerManager.StopMovementForTime(0.4f);
            playerManager.transform.LookAt(target.transform);
            playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);
            wasMoveSuccessful = true;

            GlaceBasicAtk basicAtk = playerManager.MovesController.BasicAttack as GlaceBasicAtk;
            basicAtk.FillChargeAmount();
        }
        Aim.Instance.HideAutoAim();
        Debug.Log("Finished Tail Whip!");
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideAutoAim();
    }
}