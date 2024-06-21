using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoltBasicAtk : BasicAttackBase
{
    private static readonly StatusType[] invulnerableStatuses = { StatusType.Invincible, StatusType.Untargetable, StatusType.Invisible };

    private DamageInfo normalDamage = new DamageInfo(0, 0.4f, 5, 120, DamageType.Physical);
    private DamageInfo boostedDamage = new DamageInfo(0, 0.6f, 7, 160, DamageType.Physical);

    private int charge;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 2.5f;
        normalDamage.attackerId = playerManager.NetworkObjectId;
        boostedDamage.attackerId = playerManager.NetworkObjectId;
        charge = 0;
    }

    public override void Perform()
    {
        GameObject closestEnemy = Aim.Instance.AimInCircle(range);
        Collider[] hitColliders;
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

        hitColliders = Physics.OverlapSphere(playerManager.transform.position + (playerManager.transform.forward * 1.377f), 1f);

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == playerManager.gameObject)
            {
                continue;
            }

            Pokemon targetPokemon = hitCollider.GetComponent<Pokemon>();

            if (targetPokemon == null)
            {
                continue;
            }

            if (targetPokemon.HasAnyStatusEffect(invulnerableStatuses))
            {
                continue;
            }

            if (targetPokemon.TryGetComponent(out PlayerManager player))
            {
                if (player.OrangeTeam == playerManager.OrangeTeam)
                {
                    continue;
                }
            }

            DamageInfo damageInfo = charge == 2 ? boostedDamage : normalDamage;

            targetPokemon.TakeDamage(damageInfo);
        }

        string animation = playerManager.Pokemon.CurrentLevel >= 3 ? $"ani_atk{charge + 1}_bat_0471" : $"ani_atk{charge + 4}_bat_0133";
        playerManager.AnimationManager.PlayAnimation(animation);
        playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown());

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
