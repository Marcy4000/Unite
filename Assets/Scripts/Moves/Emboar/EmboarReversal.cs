using System.Collections;
using UnityEngine;

public class EmboarReversal : MoveBase
{
    private float distance = 6f;
    private Vector3 direction;

    private string projectilePath = "Assets/Prefabs/Objects/Moves/Emboar/EmboarReversal.prefab";

    private DamageInfo damageInfo = new DamageInfo(0, 1.86f, 9, 480, DamageType.Physical);
    private StatusEffect unstoppable = new StatusEffect(StatusType.Unstoppable, 1.2f, true, 0);

    private Coroutine moveCoroutine;

    public EmboarReversal()
    {
        Name = "Reversal";
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
            playerManager.Pokemon.AddStatusEffect(unstoppable);

            if (IsUpgraded)
            {
                playerManager.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.RoundToInt(playerManager.Pokemon.GetMaxHp() * 0.1f), 0, 0, 4f, true));
            }

            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSkillshotAim();
        base.Finish();
    }

    private IEnumerator ShootProjectile()
    {
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        playerManager.AnimationManager.PlayAnimation("Fight_no_touch_attack");
        playerManager.StopMovementForTime(1f);

        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        yield return new WaitForSeconds(0.38f);

        Vector2 direction2D = new Vector2(direction.x, direction.z);
        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            if (obj.TryGetComponent(out CresseliaPsybeamProjectile projectile))
            {
                DamageInfo actualDamage = damageInfo;
                actualDamage.baseDmg = (short)Mathf.RoundToInt(actualDamage.baseDmg + (playerManager.Pokemon.GetMissingHp() / 100f));
                projectile.InitializeRPC(direction2D, actualDamage);
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
