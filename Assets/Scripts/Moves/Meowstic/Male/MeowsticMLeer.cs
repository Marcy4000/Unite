using System.Collections;
using UnityEngine;

public class MeowsticMLeer : MoveBase
{
    private float range = 3f;

    private Vector3 direction;
    private StatChange defReduction = new StatChange(20, Stat.Defense, 3f, true, false, true, 0);
    private StatChange spDefReduction = new StatChange(20, Stat.SpDefense, 3f, true, false, true, 0);

    public MeowsticMLeer()
    {
        Name = "Leer";
        Cooldown = 7f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
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

        playerManager.AnimationManager.PlayAnimation("Armature_pm0733_00_ba21_tokusyu01");
        playerManager.StopMovementForTime(1f);
        playerManager.transform.rotation = Quaternion.LookRotation(direction);

        yield return new WaitForSeconds(0.38f);

        GameObject[] hitColliders = Aim.Instance.AimInCircleAtPosition(playerManager.transform.position + (direction * 1.377f * (range / 2.5f)), 1f * (range / 2.5f), AimTarget.NonAlly);

        foreach (var hitCollider in hitColliders)
        {
            Pokemon targetPokemon = hitCollider.GetComponent<Pokemon>();

            if (targetPokemon == null)
            {
                continue;
            }

            targetPokemon.AddStatChange(defReduction);
            targetPokemon.AddStatChange(spDefReduction);
        }

        playerManager.MovesController.UnlockEveryAction();
    }

    public override void Cancel()
    {
        Aim.Instance.HideSkillshotAim();
        base.Cancel();
    }
}
