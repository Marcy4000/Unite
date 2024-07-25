using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class GlaceIcyWind : MoveBase
{
    private DamageInfo icicleDamage = new DamageInfo(0, 0.20f, 2, 35, DamageType.Special);

    private string attackPrefab = "Assets/Prefabs/Objects/BasicAtk/GlaceonBasicAtk.prefab";

    private float dashDistance = 4f;
    private float attackRange = 8f;
    private GlaceonPassive glaceonPassive;
    private GlaceBasicAtk basicAtk;

    private Vector3 dashDirection;

    private bool subscribed = false;

    private bool effectActivated = false;
    private float effectTimer = 0f;

    public GlaceIcyWind()
    {
        Name = "Icy Wind";
        Cooldown = 6.5f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        icicleDamage.attackerId = controller.Pokemon.NetworkObjectId;
        glaceonPassive = playerManager.PassiveController.Passive as GlaceonPassive;
        basicAtk = playerManager.MovesController.BasicAttack as GlaceBasicAtk;
        Aim.Instance.InitializeDashAim(dashDistance);

        if (!subscribed)
        {
            playerManager.MovesController.onBasicAttackPerformed += OnBasicAttack;
            subscribed = true;
        }
    }

    public override void Update()
    {
        if (effectTimer > 0)
        {
            effectTimer -= Time.deltaTime;
        }

        if (effectTimer <= 0 && effectActivated)
        {
            effectActivated = false;
            basicAtk.ChangeBasicAtkType(0);
            playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        }

        if (!IsActive)
        {
            return;
        }

        dashDirection = Aim.Instance.DashAim();
    }

    private void OnBasicAttack()
    {
        if (!effectActivated)
        {
            return;
        }

        GameObject closestEnemy = Aim.Instance.AimInCircle(attackRange);

        // If an enemy is found, launch a homing projectile towards it
        if (closestEnemy != null)
        {
            playerManager.StartCoroutine(LaunchProjectiles(closestEnemy.GetComponent<NetworkObject>()));

            effectActivated = false;
            basicAtk.ChangeBasicAtkType(0);
            playerManager.StopMovementForTime(0.25f);
            playerManager.AnimationManager.PlayAnimation("ani_spell1b_bat_0471");
            playerManager.transform.LookAt(closestEnemy.transform);
            playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);

            playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        }
    }

    private IEnumerator LaunchProjectiles(NetworkObject closestEnemy)
    {
        for (int i = 0; i < glaceonPassive.IciclesCount; i++)
        {
            playerManager.MovesController.LaunchProjectileFromPath(closestEnemy.NetworkObjectId, icicleDamage, attackPrefab);
            yield return new WaitForSeconds(0.01f);
        }

        byte iciclesCount = glaceonPassive.IciclesCount;
        glaceonPassive.UpdateIciclesCount(0);

        yield return new WaitForSeconds(0.45f);

        glaceonPassive.UpdateIciclesCount(iciclesCount);
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.PlayerMovement.StartDash(dashDirection);
            playerManager.AnimationManager.PlayAnimation($"ani_spell2b_bat_0471");
            playerManager.transform.rotation = Quaternion.LookRotation(dashDirection);
            basicAtk.ChangeBasicAtkType(2);
            effectActivated = true;
            effectTimer = 5f;
            wasMoveSuccessful = true;

            if (playerManager.MovesController.GetMove(MoveType.MoveB) is GlaceIceShard)
            {
                playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Disabled);
            }

            if (glaceonPassive.IciclesCount < 1)
            {
                glaceonPassive.UpdateIciclesCount(2);
            }

            glaceonPassive.ResetTimer();
        }
        Aim.Instance.HideDashAim();
        base.Finish();
    }

    public override void Cancel()
    {
        Aim.Instance.HideDashAim();
        base.Cancel();
    }

    public override void ResetMove()
    {
        if (effectActivated)
        {
            effectTimer = 0;
        }
    }
}
