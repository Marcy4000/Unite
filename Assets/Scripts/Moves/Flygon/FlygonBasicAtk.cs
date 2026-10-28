using UnityEngine;

public class FlygonBasicAtk : ChargedBasicAttackBase
{
    protected override int AttackCharge => charge;
    private static readonly StatusType[] invulnerableStatuses = { StatusType.Invincible, StatusType.Untargetable, StatusType.Invisible };

    private DamageInfo normalDamage = new DamageInfo(0, 0.35f, 5, 100, DamageType.Physical, DamageProprieties.IsBasicAttack);
    private DamageInfo boostedDamage = new DamageInfo(0, 0.45f, 6, 130, DamageType.Physical, DamageProprieties.IsBasicAttack);

    private int charge;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 2.5f;
        normalDamage.attackerId = playerManager.NetworkObjectId;
        boostedDamage.attackerId = playerManager.NetworkObjectId;
        playerManager.Pokemon.OnEvolution += OnEvolution;
        charge = 0;
    }

    public override void Perform(bool wildPriority)
    {
        PokemonType priority = wildPriority ? PokemonType.Wild : PokemonType.Player;
        GameObject closestEnemy = playerManager.Aim.AimInCircle(range, priority);
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

        hitColliders = playerManager.Aim.AimInCircleAtPosition(playerManager.transform.position + (playerManager.transform.forward * 1.377f * (range / 2.5f)), 1f * (range / 2.5f), AimTarget.NonAlly);

        foreach (var hitCollider in hitColliders)
        {
            Pokemon targetPokemon = hitCollider.GetComponent<Pokemon>();

            if (targetPokemon == null || !CombatTargeting.WithinMeleeRange(playerManager.transform.position, targetPokemon.transform.position, range))
            {
                continue;
            }

            DamageInfo damageInfo = charge == 2 ? boostedDamage : normalDamage;

            targetPokemon.TakeDamageRPC(damageInfo);
        }

        string animation = playerManager.Pokemon.CurrentLevel >= 3 ? $"ani_atk{charge + 1}_bat_0471" : $"ani_atk{charge + 4}_bat_0133";
        playerManager.AnimationManager.PlayAnimation(animation);
        playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown(), false);

        charge++;
        if (charge > 2)
        {
            charge = 0;
        }
    }

    private void OnEvolution()
    {
        if (playerManager.Pokemon.CurrentLevel == 3) range = 3f;
        else if (playerManager.Pokemon.CurrentLevel == 6) range = 3.5f;
    }

    public override void Dispose()
    {
        if (playerManager != null && playerManager.Pokemon != null) playerManager.Pokemon.OnEvolution -= OnEvolution;
        base.Dispose();
    }
}
