using System.Collections;
using UnityEngine;

public class MarshadowDrainPunch : MoveBase
{
    // The user strikes in an area dealing damage and healing the user.
    private DamageInfo damage = new DamageInfo(0, 0.5f, 6, 255, DamageType.Physical);
    private float range = 2.9f;

    private Vector3 direction;

    private Coroutine damageRoutine;

    public MarshadowDrainPunch()
    {
        Name = "Drain Punch";
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
        playerManager.AnimationManager.PlayAnimation("pm0883_ba20_buturi01");
        playerManager.StopMovementForTime(1.92f, false);
        playerManager.transform.rotation = Quaternion.LookRotation(direction);

        yield return new WaitForSeconds(0.53f);

        GameObject[] hitColliders = Aim.Instance.AimInCircleAtPosition(playerManager.transform.position + (direction * 1.377f * (range / 2.5f)), 1f * (range / 2.5f), AimTarget.NonAlly);

        int healAmount = 0;

        foreach (var hitCollider in hitColliders)
        {
            Pokemon targetPokemon = hitCollider.GetComponent<Pokemon>();

            if (targetPokemon == null)
            {
                continue;
            }

            targetPokemon.TakeDamageRPC(damage);
            healAmount += Mathf.FloorToInt(targetPokemon.CalculateDamage(damage) * 0.1f);
        }

        if (healAmount > 0)
        {
            DamageInfo heal = new DamageInfo(playerManager.NetworkObjectId, 0, 0, (short)healAmount, DamageType.Special);
            playerManager.Pokemon.HealDamageRPC(heal);
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
