using System.Collections;
using UnityEngine;

public class CresseliaPsybeam : MoveBase
{
    private float distance = 6f;
    private Vector3 direction;

    private string projectilePath = "Assets/Prefabs/Objects/Moves/Cresselia/CresseliaPsybeam.prefab";

    private DamageInfo damageInfo = new DamageInfo(0, 0.7f, 8, 430, DamageType.Special);

    private Coroutine moveCoroutine;

    public CresseliaPsybeam()
    {
        Name = "Psybeam";
        Cooldown = 7f;
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
            if (obj.TryGetComponent(out CresseliaPsybeamProjectile projectile))
            {
                projectile.InitializeRPC(direction2D, damageInfo);
                projectile.OnHit += OnMoveHit;
            }
        };
        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(projectilePath);

        yield return new WaitForSeconds(0.62f);

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }

    private void OnMoveHit()
    {
        playerManager.Pokemon.HealDamageRPC(Mathf.FloorToInt(playerManager.Pokemon.GetMaxHp() * 0.05f));
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
