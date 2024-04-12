using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CinderBasicAtk : BasicAttackBase
{
    private string attackPrefab;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 9f;
        attackPrefab = "CinderBasicAtk";
    }

    public override void Perform()
    {
        GameObject closestEnemy = Aim.Instance.AimInCircle(range);

        // If an enemy is found, launch a homing projectile towards it
        if (closestEnemy != null)
        {
            playerManager.MovesController.LaunchProjectileFromPath(closestEnemy.GetComponent<NetworkObject>().NetworkObjectId, new DamageInfo(playerManager.Pokemon.NetworkObjectId, 1, 0, 0, DamageType.Physical), attackPrefab);
            playerManager.AnimationManager.PlayAnimation("ani_atk1_bat_0815");
            playerManager.StopMovementForTime(0.8f);
        }
    }
}
