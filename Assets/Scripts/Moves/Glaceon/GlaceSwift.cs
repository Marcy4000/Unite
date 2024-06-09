using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class GlaceSwift : MoveBase
{
    private string attackPrefab;

    private DamageInfo damageInfo;
    private Vector3 direction;

    private float range = 7f;

    private float timer = 0f;
    private bool subscribed = false;
    private bool isActivated = false;
    private int starCount = 4;

    public GlaceSwift()
    {
        Name = "Swift";
        Cooldown = 5.0f;
        damageInfo = new DamageInfo(0, 0.16f, 6, 110, DamageType.Special);
        attackPrefab = "BasicAtk/CinderBasicAtk";
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        if (!subscribed)
        {
            playerManager.MovesController.onBasicAttackPerformed += OnBasicAttack;
            subscribed = true;
        }
        wasMoveSuccessful = true;
        isActivated = true;
        timer = 5f;
        starCount = 4;
    }

    public override void Update()
    {
        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }

        if (timer <= 0 && isActivated)
        {
            isActivated = false;
        }
    }

    private void OnBasicAttack()
    {
        if (!isActivated || starCount <= 0)
        {
            return;
        }

        GlaceBasicAtk basicAtk = playerManager.MovesController.BasicAttack as GlaceBasicAtk;
        byte starAmount = 1;

        if (basicAtk.Charge == 2)
        {
            starAmount = 2;
        }

        for (int i = 0; i < starAmount; i++)
        {
            GameObject closestEnemy = Aim.Instance.AimInCircle(range);

            // If an enemy is found, launch a homing projectile towards it
            if (closestEnemy != null)
            {
                playerManager.MovesController.LaunchProjectileFromPath(closestEnemy.GetComponent<NetworkObject>().NetworkObjectId, damageInfo, attackPrefab);
            }

            starCount--;

            if (starCount <= 0)
            {
                break;
            }
        }
    }
}
