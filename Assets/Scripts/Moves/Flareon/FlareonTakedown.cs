using DG.Tweening;
using System.Collections;
using UnityEngine;

public class FlareonTakedown : MoveBase
{
    private static float STUN_DURATION = 1.6f;

    private GameObject target;
    private DamageInfo normalDamage = new DamageInfo(0, 1.7f, 6, 400, DamageType.Physical, DamageProprieties.CanCrit);
    private DamageInfo boostedDamage = new DamageInfo(0, 2.1f, 7, 450, DamageType.Physical, DamageProprieties.CanCrit);

    private StatusEffect stun = new StatusEffect(StatusType.Incapacitated, STUN_DURATION, true, 0);
    private StatusEffect secondStun = new StatusEffect(StatusType.Incapacitated, 0.2f, true, 0);

    private float distance = 4.5f;
    private float angle = 45f;

    private Coroutine firstJumpRoutine;
    private Coroutine secondJumpRoutine;

    private bool hasHitTarget;
    private float secondUseTimer;

    public FlareonTakedown()
    {
        Name = "Takedown";
        Cooldown = 7f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        normalDamage.attackerId = controller.Pokemon.NetworkObjectId;
        boostedDamage.attackerId = controller.Pokemon.NetworkObjectId;

        if (IsUpgraded)
        {
            STUN_DURATION = 1.8f;
            stun.Duration = STUN_DURATION;
        }

        if (hasHitTarget)
        {
            Aim.Instance.InitializeSimpleCircle(8);
        }
        else
        {
            Aim.Instance.InitializeAutoAim(distance, angle, AimTarget.NonAlly);
        }
    }

    public override void Update()
    {
        if (hasHitTarget) {
            secondUseTimer -= Time.deltaTime;
            if (secondUseTimer <= 0)
            {
                hasHitTarget = false;
                wasMoveSuccessful = true;
                Finish();
            }
        }

        if (!IsActive)
        {
            return;
        }

        if (!hasHitTarget)
        {
            target = Aim.Instance.SureHitAim();
        }
    }

    public override void Finish()
    {
        if (target != null && IsActive && !hasHitTarget)
        {
            Pokemon targetPokemon = target.GetComponent<Pokemon>();
            targetPokemon.OnDeath += OnTargetDeath;

            firstJumpRoutine = playerManager.StartCoroutine(DoFirstDash());
            wasMoveSuccessful = false;
        }
        else if (IsActive && hasHitTarget)
        {
            BattleUIManager.instance.ShowMoveSecondaryCooldown(0, 0);
            secondJumpRoutine = playerManager.StartCoroutine(DoSecondJump());
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideAutoAim();
        base.Finish();
    }

    private IEnumerator DoFirstDash()
    {
        playerManager.PlayerMovement.CanMove = false;
        playerManager.MovesController.LockEveryAction();
        playerManager.transform.LookAt(target.transform);
        playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);
        playerManager.AnimationManager.PlayAnimation($"pm0136_fi21_run01");

        yield return playerManager.transform.DOMove(target.transform.position, 0.45f).SetEase(Ease.Linear).WaitForCompletion();

        playerManager.AnimationManager.PlayAnimation("pm0136_ba21_tokusyu02");

        GameObject[] hitEnemies = Aim.Instance.AimInCircleAtPosition(target.transform.position, 1f, AimTarget.NonAlly);

        foreach (GameObject enemy in hitEnemies)
        {
            if (enemy.TryGetComponent(out Pokemon pokemon))
            {
                if (pokemon.HasStatusEffect(StatusType.Burned))
                {
                    pokemon.TakeDamageRPC(boostedDamage);
                }
                else
                {
                    pokemon.TakeDamageRPC(normalDamage);
                }

                if (enemy == target)
                {
                    pokemon.ApplyKnockupRPC(1f, STUN_DURATION);
                    pokemon.AddStatusEffect(stun);
                }
            }
        }

        yield return new WaitForSeconds(0.1f);

        if (target != null)
        {
            target.GetComponent<Pokemon>().OnDeath -= OnTargetDeath;
        }

        playerManager.AnimationManager.SetTrigger("Transition");

        hasHitTarget = true;
        secondUseTimer = STUN_DURATION - 0.1f;

        BattleUIManager.instance.ShowMoveSecondaryCooldown(0, secondUseTimer);

        playerManager.PlayerMovement.CanMove = true;
        playerManager.MovesController.UnlockEveryAction();
    }

    private IEnumerator DoSecondJump()
    {
        playerManager.PlayerMovement.CanMove = false;
        playerManager.MovesController.LockEveryAction();
        playerManager.transform.LookAt(target.transform);
        playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);
        playerManager.AnimationManager.PlayAnimation($"pm0136_ba01_landA01");

        yield return playerManager.transform.DOJump(target.transform.position, 1.5f, 1, 0.3f).SetEase(Ease.Linear).WaitForCompletion();

        playerManager.AnimationManager.SetTrigger("Transition");

        if (target.TryGetComponent(out Pokemon pokemon))
        {
            pokemon.TakeDamageRPC(normalDamage);
            pokemon.AddStatusEffect(secondStun);
        }

        yield return new WaitForSeconds(0.1f);

        if (target != null)
        {
            target.GetComponent<Pokemon>().OnDeath -= OnTargetDeath;
        }

        hasHitTarget = false;
        secondUseTimer = 0f;

        playerManager.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.RoundToInt(playerManager.Pokemon.GetMaxHp() * 0.05f), 0, 0, 3f, true));
        playerManager.PlayerMovement.CanMove = true;
        playerManager.MovesController.UnlockEveryAction();
    }

    private void OnTargetDeath(DamageInfo info)
    {
        target.GetComponent<Pokemon>().OnDeath -= OnTargetDeath;
        ResetMove();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideAutoAim();
    }

    public override void ResetMove()
    {
        target = null;
        if (firstJumpRoutine != null)
        {
            playerManager.StopCoroutine(firstJumpRoutine);
        }

        if (secondJumpRoutine != null)
        {
            playerManager.StopCoroutine(secondJumpRoutine);
        }

        playerManager.transform.DOKill();
        playerManager.PlayerMovement.CanMove = true;
        playerManager.MovesController.UnlockEveryAction();
    }
}
