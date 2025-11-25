using System.Collections;
using UnityEngine;

public class MeowsticMScratch : MoveBase
{
    private DamageInfo damage = new DamageInfo(0, 0.45f, 5, 160, DamageType.Special);
    private float range = 2.5f;

    private Vector3 direction;

    public MeowsticMScratch()
    {
        Name = "Scratch";
        Cooldown = 6f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damage.attackerId = playerManager.NetworkObjectId;
        Aim.Instance.InitializeSkillshotAim(range);
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }

        direction = Aim.Instance.SkillshotAim();
    }

    public override void Finish()
    {
        if (IsActive && direction.magnitude != 0)
        {
            playerManager.StartCoroutine(AimRoutine());
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSkillshotAim();
        base.Finish();
    }

    private IEnumerator AimRoutine()
    {
        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        playerManager.AnimationManager.PlayAnimation("Armature_pm0733_00_ba20_buturi01");
        playerManager.StopMovementForTime(0.6f);
        playerManager.transform.rotation = Quaternion.LookRotation(direction);

        yield return new WaitForSeconds(0.32f);

        GameObject[] hitColliders = Aim.Instance.AimInCircleAtPosition(playerManager.transform.position + (direction * 1.377f * (range / 2.5f)), 1f * (range / 2.5f), AimTarget.NonAlly);

        foreach (var hitCollider in hitColliders)
        {
            Pokemon targetPokemon = hitCollider.GetComponent<Pokemon>();

            if (targetPokemon == null)
            {
                continue;
            }

            playerManager.StartCoroutine(DamageRoutine(targetPokemon));
        }

        yield return new WaitForSeconds(0.285f);

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }

    private IEnumerator DamageRoutine(Pokemon pokemon)
    {
        if (pokemon == null)
        {
            yield break;
        }

        pokemon.TakeDamageRPC(damage);

        yield return new WaitForSeconds(0.16f);

        if (pokemon == null)
        {
            yield break;
        }

        pokemon.TakeDamageRPC(damage);
    }

    public override void Cancel()
    {
        Aim.Instance.HideSkillshotAim();
        base.Cancel();
    }

    public override void ResetMove()
    {
        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }
}
