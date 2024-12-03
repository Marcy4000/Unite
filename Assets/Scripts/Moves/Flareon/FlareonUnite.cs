using DG.Tweening;
using System.Collections;
using UnityEngine;

public class FlareonUnite : MoveBase
{
    private GameObject target;
    private DamageInfo damageInfo = new DamageInfo(0, 2.5f, 7, 510, DamageType.Physical, DamageProprieties.CanCrit);

    private StatusEffect invulnerableEffect = new StatusEffect(StatusType.Invincible, 0.75f, true, 0);
    private StatusEffect burnEffect = new StatusEffect(StatusType.Burned, 3f, true, 0);

    private float distance = 5f;
    private float angle = 45f;

    private Coroutine uniteMoveCoroutine;

    public FlareonUnite()
    {
        Name = "Fluffy Flare Crash";
        Cooldown = 0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        Aim.Instance.InitializeAutoAim(distance, angle, AimTarget.NonAlly);
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }
        target = Aim.Instance.SureHitAim();
    }

    public override void Finish()
    {
        if (target != null && IsActive)
        {
            Pokemon targetPokemon = target.GetComponent<Pokemon>();
            targetPokemon.OnDeath += OnTargetDeath;

            uniteMoveCoroutine = playerManager.StartCoroutine(DoUniteMove());
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideAutoAim();
        base.Finish();
    }

    private IEnumerator DoUniteMove()
    {
        playerManager.PlayerMovement.CanMove = false;
        playerManager.MovesController.LockEveryAction();
        playerManager.Pokemon.AddStatusEffect(invulnerableEffect);
        playerManager.transform.LookAt(target.transform);
        playerManager.transform.eulerAngles = new Vector3(0, playerManager.transform.eulerAngles.y, 0);
        playerManager.AnimationManager.PlayAnimation($"pm0136_ba01_uniteMove");

        yield return new WaitForSeconds(0.25f);

        yield return playerManager.transform.DOMove(target.transform.position, 0.3f).SetEase(Ease.Linear).WaitForCompletion();

        playerManager.AnimationManager.SetTrigger("Transition");

        GameObject[] hitEnemies = Aim.Instance.AimInCircleAtPosition(target.transform.position, 2f, AimTarget.NonAlly);

        foreach (GameObject enemy in hitEnemies)
        {
            if (enemy.TryGetComponent(out Pokemon pokemon))
            {
                pokemon.TakeDamageRPC(damageInfo);
                pokemon.TakeDamageRPC(new DamageInfo(playerManager.NetworkObjectId, 0f, 0, (short)Mathf.RoundToInt(pokemon.GetMissingHp() * 0.05f), DamageType.Physical));
                pokemon.AddStatusEffect(burnEffect);
            }
        }

        yield return new WaitForSeconds(0.2f);

        if (target != null)
        {
            target.GetComponent<Pokemon>().OnDeath -= OnTargetDeath;
        }

        playerManager.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.RoundToInt(playerManager.Pokemon.GetMaxHp() * 0.12f), 0, 0, 3f, true));
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
        if (uniteMoveCoroutine != null)
        {
            playerManager.StopCoroutine(uniteMoveCoroutine);
        }
        playerManager.transform.DOKill();
        playerManager.PlayerMovement.CanMove = true;
        playerManager.MovesController.UnlockEveryAction();
    }
}
