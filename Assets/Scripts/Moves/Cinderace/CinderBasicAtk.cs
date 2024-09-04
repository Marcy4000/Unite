using Unity.Netcode;
using UnityEngine;

public class CinderBasicAtk : BasicAttackBase
{
    private string attackPrefab;
    private byte charge = 0;

    private DamageInfo normalDmg;
    private DamageInfo boostedDmg;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 7f;
        attackPrefab = "Assets/Prefabs/Objects/BasicAtk/CinderBasicAtk.prefab";
        DamageProprieties proprieties = DamageProprieties.IsBasicAttack;
        proprieties |= DamageProprieties.CanCrit;
        normalDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 1, 0, 0, DamageType.Physical, proprieties);
        boostedDmg = new DamageInfo(playerManager.Pokemon.NetworkObjectId, 1.3f, 0, 0, DamageType.Physical, proprieties);
        manager.MovesController.onMovePerformed += OnMovePerformed;
    }

    private void OnMovePerformed(MoveBase move)
    {
        charge = 2;
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
            playerManager.AnimationManager.PlayAnimation($"ani_atk{charge+1}_bat_0815");
            playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown());
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
