using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class SerperiorWringOut : MoveBase
{
    private GameObject target;
    private DamageInfo damageInfo = new DamageInfo(0, 0.8f, 3, 300, DamageType.Physical, DamageProprieties.CanCrit);
    private StatusEffect holdState = new StatusEffect(StatusType.Bound, 3.5f, true, 60);

    private Pokemon targetPokemon;

    private float range = 6f;
    private bool isHoldingTarget = false;
    private float holdTimer = 0f;
    private float holdDamageTick = 0.5f;

    private bool subscribed = false;

    public SerperiorWringOut()
    {
        Name = "Wring Out";
        Cooldown = 10.0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeAutoAim(range, 60f, AimTarget.NonAlly);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;

        if (!subscribed)
        {
            playerManager.Pokemon.OnDeath += OnPlayerDeath;
            subscribed = true;
        }
    }

    public override void Update()
    {
        if (isHoldingTarget)
        {
            holdDamageTick -= Time.deltaTime;
            holdTimer += Time.deltaTime;

            if (holdDamageTick <= 0f)
            {
                if (targetPokemon != null)
                {
                    targetPokemon.TakeDamageRPC(damageInfo);
                    int healAmount = Mathf.FloorToInt(targetPokemon.CalculateDamage(damageInfo) * 0.5f);
                    playerManager.Pokemon.HealDamageRPC(healAmount);
                }
                holdDamageTick = 0.5f;
            }

            if (holdTimer >= 3.5f)
            {
                if (targetPokemon != null)
                {
                    targetPokemon.TakeDamageRPC(damageInfo);
                    targetPokemon.RemoveStatusEffectWithID(holdState.ID);
                    targetPokemon.OnDeath -= OnTargetDeath;
                }
                
                playerManager.MovesController.UnlockEveryAction();
                playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
                playerManager.PlayerMovement.CanMove = true;
                isHoldingTarget = false;
                holdTimer = 0f;
                targetPokemon = null;

                wasMoveSuccessful = true;
                Finish();
            }
        }

        if (!IsActive)
        {
            return;
        }
        target = Aim.Instance.SureHitAim();
    }

    public override void Finish()
    {
        if (IsActive && target != null)
        {
            playerManager.PlayerMovement.CanMove = false;
            targetPokemon = target.GetComponent<Pokemon>();

            targetPokemon.OnDeath += OnTargetDeath;

            playerManager.MovesController.LockEveryAction();
            playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

            playerManager.transform.DOMove(target.transform.position, 0.2f).OnComplete(() =>
            {
                targetPokemon.AddStatusEffect(holdState);
                isHoldingTarget = true;
            });

            playerManager.AnimationManager.PlayAnimation($"Fight_no_touch_attack_3");
            playerManager.AnimationManager.SetBool("Walking", false);
            playerManager.transform.rotation = Quaternion.LookRotation(target.transform.position - playerManager.transform.position);
            playerManager.transform.rotation = Quaternion.Euler(0, playerManager.transform.rotation.eulerAngles.y, 0);
            wasMoveSuccessful = false;
        }
        Aim.Instance.HideAutoAim();
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideAutoAim();
    }

    public override void ResetMove()
    {
        if (targetPokemon != null)
        {
            targetPokemon.RemoveStatusEffectWithID(holdState.ID);
            targetPokemon = null;
        }

        isHoldingTarget = false;
        holdTimer = 0f;
        holdDamageTick = 0.5f;
    }

    private void OnPlayerDeath(DamageInfo damage)
    {
        ResetMove();
    }

    private void OnTargetDeath(DamageInfo damage)
    {
        holdTimer = 3.5f;
    }
}
