using Unity.Netcode;
using UnityEngine;

public class MeowsticMBasicAtk : BasicAttackBase
{
    private string attackPrefab;
    private byte charge = 0;

    private DamageInfo normalDmg;
    private DamageInfo boostedDmg;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 4f;
        attackPrefab = "Assets/Prefabs/Objects/BasicAtk/CinderBasicAtk.prefab";
        normalDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 1f, 0, 0, DamageType.Physical, DamageProprieties.IsBasicAttack);
        boostedDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 1.1f, 3, 70, DamageType.Special, DamageProprieties.IsBasicAttack);
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
            playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown(), false);
            playerManager.transform.LookAt(closestEnemy.transform);
            playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);
            charge++;
        }

        if (charge > 2)
        {
            charge = 0;
        }
    }

    public override void Update()
    {
    }
}
