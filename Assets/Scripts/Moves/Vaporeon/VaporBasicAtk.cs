using Unity.Netcode;
using UnityEngine;

public class VaporBasicAtk : BasicAttackBase
{
    private string attackPrefab;
    private byte charge = 0;

    private float cooldown;

    private DamageInfo normalDmg;
    private DamageInfo boostedDmg;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 6f;
        attackPrefab = "Assets/Prefabs/Objects/BasicAtk/CinderBasicAtk.prefab";
        normalDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 0.25f, 8, 170, DamageType.Special);
        boostedDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 0.36f, 11, 180, DamageType.Special);
    }

    public override void Perform(bool wildPriority)
    {
        PokemonType priority = wildPriority ? PokemonType.Wild : PokemonType.Player;
        GameObject closestEnemy = Aim.Instance.AimInCircle(range, priority);

        // If an enemy is found, launch a homing projectile towards it
        if (closestEnemy != null)
        {
            DamageInfo damage = charge == 2 ? boostedDmg : normalDmg;

            playerManager.MovesController.LaunchProjectileFromPath(closestEnemy.GetComponent<NetworkObject>().NetworkObjectId, damage, attackPrefab);
            playerManager.AnimationManager.PlayAnimation($"ani_atk{charge + 4}_bat_0133");
            playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown(), playerManager.Pokemon.CurrentLevel < 3);
            playerManager.transform.LookAt(closestEnemy.transform);
            playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);
            cooldown = 4.5f;

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
