using System.Collections;
using UnityEngine;

public class FlygonBite : MoveBase
{
    private DamageInfo damage = new DamageInfo(0, 0.45f, 6, 150, DamageType.Physical);
    private float range = 3f;

    private Vector3 direction;

    private Coroutine damageRoutine;

    public FlygonBite()
    {
        Name = "Bite";
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
            damageRoutine = playerManager.StartCoroutine(DamageRoutine());
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSkillshotAim();
        base.Finish();
    }

    private IEnumerator DamageRoutine()
    {
        string animation = playerManager.Pokemon.CurrentLevel >= 3 ? "Armature_pm0329_00_ba20_buturi01" : "Armature_pm0328_ba20_buturi01";
        playerManager.AnimationManager.PlayAnimation(animation);
        playerManager.StopMovementForTime(1f);
        playerManager.transform.rotation = Quaternion.LookRotation(direction);

        yield return new WaitForSeconds(0.41f);

        GameObject[] hitColliders = Aim.Instance.AimInCircleAtPosition(playerManager.transform.position + (direction * 1.377f * (range / 2.5f)), 1f * (range / 2.5f), AimTarget.NonAlly);

        foreach (var hitCollider in hitColliders)
        {
            Pokemon targetPokemon = hitCollider.GetComponent<Pokemon>();

            if (targetPokemon == null)
            {
                continue;
            }

            targetPokemon.TakeDamageRPC(damage);
        }
    }

    public override void Cancel()
    {
        Aim.Instance.HideSkillshotAim();
        base.Cancel();
    }

    public override void ResetMove()
    {
        if (damageRoutine != null)
        {
            playerManager.StopCoroutine(damageRoutine);
        }
    }

}
