using UnityEngine;

public class EmboarBasicAtk : BasicAttackBase
{
    private DamageInfo normalDamage = new DamageInfo(0, 0.4f, 5, 120, DamageType.Physical, DamageProprieties.IsBasicAttack);
    private DamageInfo boostedDamage = new DamageInfo(0, 0.6f, 7, 160, DamageType.Physical, DamageProprieties.IsBasicAttack);

    private int charge;

    public bool nextBoostedAttackKnocksUp;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 2.5f;

        playerManager.Pokemon.OnEvolution += OnEvolution;

        DamageProprieties proprieties = DamageProprieties.IsBasicAttack;
        proprieties |= DamageProprieties.CanCrit;

        normalDamage = new DamageInfo(playerManager.NetworkObjectId, 0.4f, 5, 120, DamageType.Physical, proprieties);
        boostedDamage = new DamageInfo(playerManager.NetworkObjectId, 0.6f, 7, 160, DamageType.Physical, proprieties);

        charge = 0;
    }

    private void OnEvolution()
    {
        if (playerManager.Pokemon.CurrentLevel == 4)
        {
            range = 3;
        }
        else if (playerManager.Pokemon.CurrentLevel == 6)
        {
            range = 3.5f;
        }
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

        float offsetMultiplier = range / 2.5f;
        Vector3 offsetPosition = playerManager.transform.position + (playerManager.transform.forward * 1.377f * offsetMultiplier);
        hitColliders = Aim.Instance.AimInCircleAtPosition(offsetPosition, 1f * offsetMultiplier, AimTarget.NonAlly);

        foreach (var hitCollider in hitColliders)
        {
            Pokemon targetPokemon = hitCollider.GetComponent<Pokemon>();

            if (targetPokemon == null)
            {
                continue;
            }

            DamageInfo damageInfo = charge == 2 ? boostedDamage : normalDamage;

            targetPokemon.TakeDamageRPC(damageInfo);

            if (charge == 2 && nextBoostedAttackKnocksUp)
            {
                targetPokemon.ApplyKnockupRPC(1f, 1f);
                targetPokemon.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 1f, true, 0));
                nextBoostedAttackKnocksUp = false;
            }
        }

        playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown(), false);

        charge++;
        if (charge > 2)
        {
            charge = 0;
        }
    }

    public void SetCharge(int charge)
    {
        this.charge = Mathf.Clamp(charge, 0, 2);
    }

    public override void Update()
    {

    }
}
