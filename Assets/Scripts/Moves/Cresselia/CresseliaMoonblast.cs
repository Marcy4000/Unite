using System.Collections;
using UnityEngine;

public class CresseliaMoonblast : MoveBase
{
    private float distance = 6.5f;
    private Vector3 direction;

    private string projectilePath = "Assets/Prefabs/Objects/Moves/Cresselia/CresseliaMoonblast.prefab";

    private DamageInfo damageInfo = new DamageInfo(0, 0.67f, 7, 400, DamageType.Special);

    private Coroutine moveCoroutine;

    public CresseliaMoonblast()
    {
        Name = "Moonblast";
        Cooldown = 9f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeSkillshotAim(distance);
        damageInfo.attackerId = playerManager.NetworkObjectId;
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }

        direction = Aim.Instance.SkillshotAim();
    }

    public override void Finish()
    {
        if (IsActive)
        {
            moveCoroutine = playerManager.StartCoroutine(ShootProjectile());

            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSkillshotAim();
        base.Finish();
    }

    private IEnumerator ShootProjectile()
    {
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        playerManager.AnimationManager.PlayAnimation("pm0488_ba21_tokusyu01");
        playerManager.StopMovementForTime(1f);

        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        yield return new WaitForSeconds(0.38f);

        Vector2 direction2D = new Vector2(direction.x, direction.z);
        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            if (obj.TryGetComponent(out MoonblastProjectile projectile))
            {
                projectile.SetDirectionRPC(direction2D, damageInfo, distance);
            }
        };
        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(projectilePath);

        yield return new WaitForSeconds(0.62f);

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideSkillshotAim();
    }

    public override void ResetMove()
    {
        direction = Vector3.zero;
        if (moveCoroutine != null)
        {
            playerManager.StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
    }
}
