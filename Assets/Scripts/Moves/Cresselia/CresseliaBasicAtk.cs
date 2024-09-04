using Unity.Netcode;
using UnityEngine;

public class CresseliaBasicAtk : BasicAttackBase
{
    private string attackPrefab;

    private float cooldown;
    private const float MAX_COOLDOWN = 6.5f;

    private DamageInfo normalDmg;
    private DamageInfo boostedDmg;

    private StatusEffect boostedStun = new StatusEffect(StatusType.Asleep, 0.15f, true, 0);

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 4f;
        attackPrefab = "Assets/Prefabs/Objects/BasicAtk/CinderBasicAtk.prefab";
        normalDmg = new DamageInfo(playerManager.NetworkObjectId, 1f, 0, 0, DamageType.Physical, DamageProprieties.IsBasicAttack);
        boostedDmg = new DamageInfo(playerManager.NetworkObjectId, 0.44f, 18, 280, DamageType.Special, DamageProprieties.IsBasicAttack);
        playerManager.HPBar.ShowGenericGuage(true);
    }

    public override void Perform(bool wildPriority)
    {
        PokemonType priority = wildPriority ? PokemonType.Wild : PokemonType.Player;
        GameObject closestEnemy = Aim.Instance.AimInCircle(range, priority);

        // If an enemy is found, launch a homing projectile towards it
        if (closestEnemy != null)
        {
            DamageInfo damage = cooldown <= 0 ? boostedDmg : normalDmg;

            playerManager.MovesController.LaunchProjectileFromPath(closestEnemy.GetComponent<NetworkObject>().NetworkObjectId, damage, attackPrefab);
            playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown(), false);
            playerManager.transform.LookAt(closestEnemy.transform);
            playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);

            if (cooldown <= 0)
            {
                closestEnemy.GetComponent<Pokemon>().AddStatusEffect(boostedStun);
                cooldown = MAX_COOLDOWN;
            }
        }
    }

    public override void Update()
    {
        if (cooldown > 0)
        {
            cooldown -= Time.deltaTime;
        }

        playerManager.HPBar.UpdateGenericGuageValue(MAX_COOLDOWN - cooldown, MAX_COOLDOWN);
    }
}
