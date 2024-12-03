using UnityEngine;

public class FlareonBasicAtk : BasicAttackBase
{
    private DamageInfo normalDamage = new DamageInfo(0, 1f, 2, 100, DamageType.Physical, DamageProprieties.IsBasicAttack);
    private DamageInfo boostedDamage = new DamageInfo(0, 1.3f, 3, 120, DamageType.Physical, DamageProprieties.IsBasicAttack);

    private int charge;
    private float chargeTime = 0.5f;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 2.5f;
        normalDamage.attackerId = playerManager.NetworkObjectId;
        boostedDamage.attackerId = playerManager.NetworkObjectId;

        normalDamage.proprieties |= DamageProprieties.CanCrit;
        boostedDamage.proprieties |= DamageProprieties.CanCrit;

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
            if (!hitCollider.TryGetComponent<Pokemon>(out var targetPokemon))
            {
                continue;
            }

            DamageInfo damageInfo = charge == 2 ? boostedDamage : normalDamage;

            targetPokemon.TakeDamageRPC(damageInfo);
        }

        string animation = playerManager.Pokemon.CurrentLevel >= 3 ? "" : $"ani_atk{charge + 4}_bat_0133";
        playerManager.AnimationManager.PlayAnimation(animation);
        playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown(), playerManager.Pokemon.CurrentLevel < 3);

        charge++;
        chargeTime = 5f;
        if (charge > 2)
        {
            playerManager.MovesController.ReduceMoveCooldown(MoveType.MoveA, 0.5f);
            playerManager.MovesController.ReduceMoveCooldown(MoveType.MoveB, 0.5f);
            charge = 0;
        }
    }

    public override void Update()
    {
        if (charge > 0)
        {
            chargeTime -= Time.deltaTime;
            if (chargeTime <= 0)
            {
                charge = 0;
            }
        }
    }
}
