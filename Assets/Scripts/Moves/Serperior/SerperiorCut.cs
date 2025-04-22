using System.Collections;
using UnityEngine;

public class SerperiorCut : MoveBase
{
    private DamageInfo damage = new DamageInfo(0, 1.15f, 6, 400, DamageType.Physical);
    private StatChange defDebuff = new StatChange(10, Stat.Defense, 2f, true, false, true, 0);
    private float range = 2.5f;

    private Vector3 direction;

    private Coroutine damageRoutine;

    public SerperiorCut()
    {
        Name = "Cut";
        Cooldown = 9f;
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
        playerManager.AnimationManager.PlayAnimation("Fight_no_touch_attack");
        playerManager.StopMovementForTime(0.5f);
        playerManager.transform.rotation = Quaternion.LookRotation(direction);

        yield return new WaitForSeconds(0.2f);

        GameObject[] hitColliders = Aim.Instance.AimInCircleAtPosition(playerManager.transform.position + (direction * 1.377f * (range / 2.5f)), 1f * (range / 2.5f), AimTarget.NonAlly);

        foreach (var hitCollider in hitColliders)
        {
            Pokemon targetPokemon = hitCollider.GetComponent<Pokemon>();

            if (targetPokemon == null)
            {
                continue;
            }

            targetPokemon.TakeDamageRPC(damage);
            targetPokemon.AddStatChange(defDebuff);
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
