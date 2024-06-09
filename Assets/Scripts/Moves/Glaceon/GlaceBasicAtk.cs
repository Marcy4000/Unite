using Unity.Netcode;
using UnityEngine;

public class GlaceBasicAtk : BasicAttackBase
{
    private string attackPrefab;
    private byte charge = 0;

    private float cooldown;

    private DamageInfo normalDmg;
    private DamageInfo boostedDmg;

    public byte Charge { get => charge; set => charge = value; }

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 7f;
        attackPrefab = "BasicAtk/CinderBasicAtk";
        normalDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 0.39f, 4, 70, DamageType.Special);
        boostedDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 0.47f, 5, 90, DamageType.Special);
    }

    public override void Perform()
    {
        GameObject closestEnemy = Aim.Instance.AimInCircle(range);

        // If an enemy is found, launch a homing projectile towards it
        if (closestEnemy != null)
        {
            DamageInfo damage = charge == 2 ? boostedDmg : normalDmg;

            playerManager.MovesController.LaunchProjectileFromPath(closestEnemy.GetComponent<NetworkObject>().NetworkObjectId, damage, attackPrefab);
            string animation = playerManager.Pokemon.CurrentLevel.Value >= 3 ? $"ani_atk{charge + 1}_bat_0471" : $"ani_atk{charge + 4}_bat_0133";
            playerManager.AnimationManager.PlayAnimation(animation);
            playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown());
            playerManager.transform.LookAt(closestEnemy.transform);
            playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);
            cooldown = 4f;
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
