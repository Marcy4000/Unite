using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SerperiorBasicAtk : BasicAttackBase
{
    private DamageInfo normalDamage;
    private DamageInfo boostedDamage;

    public bool nextAttackDashes = false;
    private float dashCooldownTimer = 7.5f;

    public override void Initialize(PlayerManager manager)
    {
        base.Initialize(manager);
        range = 2.25f;

        playerManager.Pokemon.OnEvolution += OnEvolution;

        DamageProprieties proprieties = DamageProprieties.IsBasicAttack;
        proprieties |= DamageProprieties.CanCrit;

        normalDamage = new DamageInfo(playerManager.NetworkObjectId, 0.45f, 4, 130, DamageType.Physical, proprieties);
        boostedDamage = new DamageInfo(playerManager.NetworkObjectId, 0.65f, 6, 175, DamageType.Physical, proprieties);

        playerManager.HPBar.ShowGenericGuage(true);
        playerManager.HPBar.UpdateGenericGuageValue(1f);
    }

    private void OnEvolution()
    {
        if (playerManager.Pokemon.CurrentLevel == 4)
        {
            range = 2.75f;
        }
        else if (playerManager.Pokemon.CurrentLevel == 6)
        {
            range = 3.25f;
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

            DamageInfo damageInfo = nextAttackDashes ? boostedDamage : normalDamage;

            targetPokemon.TakeDamageRPC(damageInfo);

            if (nextAttackDashes)
            {
                playerManager.StartCoroutine(DoDash((targetPokemon.transform.position - playerManager.transform.position).normalized, Vector3.Distance(playerManager.transform.position, targetPokemon.transform.position)));
                nextAttackDashes = false;
            }
        }

        playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown(), false);
    }

    private IEnumerator DoDash(Vector3 dashDirection, float dashRange)
    {
        playerManager.transform.LookAt(playerManager.transform.position + dashDirection);
        playerManager.transform.rotation = Quaternion.Euler(0, playerManager.transform.rotation.eulerAngles.y, 0);

        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);
        playerManager.PlayerMovement.CanMove = false;

        Vector3 spawnPos = playerManager.transform.position - (dashDirection * 0.5f);

        playerManager.transform.DOMove(playerManager.transform.position + (dashDirection * dashRange), 0.35f);

        yield return new WaitForSeconds(0.35f);

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        playerManager.PlayerMovement.CanMove = true;
    }

    public override void Update()
    {
        if (!nextAttackDashes)
        {
            dashCooldownTimer -= Time.deltaTime;
            playerManager.HPBar.UpdateGenericGuageValue(1f - (dashCooldownTimer / 7.5f));
            if (dashCooldownTimer <= 0f)
            {
                nextAttackDashes = true;
                dashCooldownTimer = 7.5f; // Reset the cooldown
                playerManager.HPBar.UpdateGenericGuageValue(1f);
            }
        }
    }
}
