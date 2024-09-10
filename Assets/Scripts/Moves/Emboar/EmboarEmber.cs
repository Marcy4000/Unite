using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class EmboarEmber : MoveBase
{
    private GameObject target;
    private DamageInfo damageInfo;
    private float distance;
    private float angle;

    private Coroutine launchProjectileCoroutine;

    public EmboarEmber()
    {
        Name = "Ember";
        Cooldown = 6.5f;
        distance = 5.5f;
        angle = 45f;
        damageInfo = new DamageInfo(0, 1.2f, 7, 155, DamageType.Physical, DamageProprieties.CanCrit);
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
            launchProjectileCoroutine = playerManager.StartCoroutine(LaunchProjectile());
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideAutoAim();
        base.Finish();
    }

    private IEnumerator LaunchProjectile()
    {
        playerManager.AnimationManager.PlayAnimation($"Fight_no_touch_attack");
        playerManager.StopMovementForTime(1.333f);
        playerManager.transform.LookAt(target.transform);
        playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);

        yield return new WaitForSeconds(0.7f);

        playerManager.MovesController.LaunchHomingProjectileRpc(target.GetComponent<NetworkObject>().NetworkObjectId, damageInfo);
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideAutoAim();
    }

    public override void ResetMove()
    {
        if (launchProjectileCoroutine != null)
        {
            playerManager.StopCoroutine(launchProjectileCoroutine);
            launchProjectileCoroutine = null;
        }

        target = null;
    }
}
