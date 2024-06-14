using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SylvBasicAtk : BasicAttackBase
{
    private string attackPrefab;
    private byte charge = 0;

    private float cooldown;

    private StatChange speedBuff = new StatChange(75, Stat.Speed, 1f, true, true, true, 0);

    private DamageInfo normalDmg;
    private DamageInfo boostedDmg;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 5f;
        attackPrefab = "BasicAtk/CinderBasicAtk";
        normalDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 1, 0, 0, DamageType.Physical);
        boostedDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 0.35f, 10, 180, DamageType.Special);
    }

    public override void Perform()
    {
        GameObject closestEnemy = Aim.Instance.AimInCircle(range);

        // If an enemy is found, launch a homing projectile towards it
        if (closestEnemy != null)
        {
            DamageInfo damage = charge == 2 ? boostedDmg : normalDmg;

            playerManager.MovesController.LaunchProjectileFromPath(closestEnemy.GetComponent<NetworkObject>().NetworkObjectId, damage, attackPrefab);
            playerManager.AnimationManager.PlayAnimation($"ani_atk{charge + 4}_bat_0133");
            playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown());
            playerManager.transform.LookAt(closestEnemy.transform);
            playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);
            cooldown = 4.5f;

            if (charge == 2 && playerManager.Pokemon.CurrentLevel.Value >= 3)
            {
                speedBuff.Duration = 1f + (0.5f * playerManager.MovesController.GetAtkSpeedCooldown());
                playerManager.Pokemon.AddStatChange(speedBuff);
            }

            charge++;
        }

        if (charge > 2)
        {
            charge = 0;
        }
    }

    public override void Update()
    {
        if (cooldown > 0)
        {
            cooldown -= Time.deltaTime;
        }

        if (cooldown <= 0 && charge > 0)
        {
            charge = 0;
        }
    }
}
