using UnityEngine;

public class MarshadowBasicAtk : BasicAttackBase
{
    private DamageInfo normalDamage = new DamageInfo(0, 0.6f, 5, 110, DamageType.Physical, DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit);
    private DamageInfo boostedDamage = new DamageInfo(0, 0.9f, 6, 140, DamageType.Physical, DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit);

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

        MarshadowMoveRuntime.BreakStealth(playerManager);
        bool hit = false;
        var unique = new System.Collections.Generic.HashSet<Pokemon>();
        foreach (var hitCollider in hitColliders)
        {
            Pokemon targetPokemon = hitCollider.GetComponent<Pokemon>();

            if (!MarshadowMoveRuntime.CanHit(playerManager, targetPokemon)
                || !CombatTargeting.WithinMeleeRange(playerManager.transform.position, targetPokemon.transform.position, range) || !unique.Add(targetPokemon))
            {
                continue;
            }

            DamageInfo damageInfo = charge == 2 ? boostedDamage : normalDamage;

            targetPokemon.TakeDamageRPC(damageInfo);
            hit = true;
        }

        if (hit && charge == 2) (playerManager.PassiveController.Passive as MarshadwPassive)?.OnBoostedHit();
        MarshadowMoveRuntime.Effect(playerManager, charge == 2 ? MarshadowEffect.BoostedPunch : MarshadowEffect.Punch, 0.18f);

        playerManager.StopMovementForTime(0.22f * playerManager.MovesController.GetAtkSpeedCooldown(), false);

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
