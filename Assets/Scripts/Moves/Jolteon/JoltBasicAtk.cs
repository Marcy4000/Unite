using UnityEngine;

public class JoltBasicAtk : BasicAttackBase
{
    private DamageInfo normalDamage = new DamageInfo(0, 0.4f, 5, 120, DamageType.Physical, DamageProprieties.IsBasicAttack);
    private DamageInfo boostedDamage = new DamageInfo(0, 0.6f, 7, 160, DamageType.Physical, DamageProprieties.IsBasicAttack);

    private int charge;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 2.5f;
        normalDamage.attackerId = playerManager.NetworkObjectId;
        boostedDamage.attackerId = playerManager.NetworkObjectId;
        charge = 0;
    }

    public override void Perform(bool wildPriority)
    {
        PokemonType priority = wildPriority ? PokemonType.Wild : PokemonType.Player;
        GameObject closestEnemy = Aim.Instance.AimInCircle(range, priority);
        GameObject[] hitColliders;
        if (closestEnemy != null)
        {
            playerManager.transform.LookAt(closestEnemy.transform);
            playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);
        }
        else
        {
            if (playerManager.PlayerMovement.IsMoving)
            {
                return;
            }
        }

        hitColliders = Aim.Instance.AimInCircleAtPosition(playerManager.transform.position + (playerManager.transform.forward * 1.377f), 1f, AimTarget.NonAlly);

        foreach (var hitCollider in hitColliders)
        {
            Pokemon targetPokemon = hitCollider.GetComponent<Pokemon>();

            if (targetPokemon == null)
            {
                continue;
            }

            DamageInfo damageInfo = charge == 2 ? boostedDamage : normalDamage;

            targetPokemon.TakeDamage(damageInfo);
        }

        string animation = playerManager.Pokemon.CurrentLevel >= 3 ? $"ani_atk{charge + 1}_bat_0471" : $"ani_atk{charge + 4}_bat_0133";
        playerManager.AnimationManager.PlayAnimation(animation);
        playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown(), playerManager.Pokemon.CurrentLevel < 3);

        charge++;
        if (charge > 2)
        {
            charge = 0;
        }
    }

    public override void Update()
    {
        
    }
}
