using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarshadowPhantomForce : MoveBase
{
    /*the user becomes ghostly and is invulnerable for a limited amount of time.
    If the user activates the move again the user will
    unleash a powerful kick throwing opponents away and stunning them if
    they collide to terrain.*/

    private StatusEffect invulnerableEffect = new StatusEffect(StatusType.Invincible, 0, false, 6);
    private DamageInfo damage = new DamageInfo(0, 1.2f, 7, 120, DamageType.Physical);

    private float range = 2.9f;

    private float secondUseTimer = 3.5f;
    private bool isSecondUse;

    private bool isFinishing;

    public MarshadowPhantomForce()
    {
        Name = "Phantom Force";
        Cooldown = 8f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damage.attackerId = playerManager.NetworkObjectId;
    }

    public override void Update()
    {
        if (isSecondUse)
        {
            if (secondUseTimer >= 0)
            {
                secondUseTimer -= Time.deltaTime;
            }

            if (secondUseTimer <= 0 && !isFinishing)
            {
                IsActive = true;
                Finish();
                isFinishing = true;
            }
        }
    }

    public override void Finish()
    {
        if (IsActive)
        {
            if (!isSecondUse)
            {
                secondUseTimer = 3.5f;
                BattleUIManager.instance.ShowMoveSecondaryCooldown(1, secondUseTimer);
                playerManager.Pokemon.AddStatusEffect(invulnerableEffect);
                isFinishing = false;
                isSecondUse = true;
            }
            else
            {
                isFinishing = true;
                isSecondUse = false;
                wasMoveSuccessful = true;
                BattleUIManager.instance.ShowMoveSecondaryCooldown(1, 0);
                playerManager.Pokemon.RemoveStatusEffectWithID(invulnerableEffect.ID);
                playerManager.StartCoroutine(DoKick());
            }
        }
        base.Finish();
    }

    private IEnumerator DoKick()
    {
        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);
        playerManager.PlayerMovement.CanMove = false;
        playerManager.AnimationManager.PlayAnimation("pm0883_ba20_buturi02");

        yield return new WaitForSeconds(0.63f);

        GameObject[] hitColliders = Aim.Instance.AimInCircleAtPosition(playerManager.transform.position + (playerManager.transform.forward * 1.377f * (range / 2.5f)), 1f * (range / 2.5f), AimTarget.NonAlly);

        foreach (var hitCollider in hitColliders)
        {
            Pokemon targetPokemon = hitCollider.GetComponent<Pokemon>();

            if (targetPokemon == null)
            {
                continue;
            }

            targetPokemon.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.2f, true, 0));
            targetPokemon.ApplyKnockbackRPC(playerManager.transform.forward, 15f);

            targetPokemon.TakeDamage(damage);
        }

        yield return new WaitForSeconds(0.97f);

        playerManager.PlayerMovement.CanMove = true;
        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }
}
