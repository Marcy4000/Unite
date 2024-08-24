using System.Collections;
using UnityEngine;

public class CinderPyroball : MoveBase
{
    private DamageInfo damageInfo;
    private float distance;
    private Vector3 direction;

    private Coroutine moveCoroutine;

    public CinderPyroball()
    {
        Name = "Pyroball";
        Cooldown = 5.0f;
        distance = 8f;
        damageInfo = new DamageInfo(0, 3.45f, 32, 820, DamageType.Physical);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        Aim.Instance.InitializeSkillshotAim(distance);
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
        if (direction.magnitude != 0 && IsActive)
        {
            moveCoroutine = playerManager.StartCoroutine(LaunchPyroball());
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSkillshotAim();
        Debug.Log($"Finished {Name}!");
        base.Finish();
    }

    private IEnumerator LaunchPyroball()
    {
        playerManager.StopMovementForTime(1.1f);
        Vector2 direction = new Vector2(this.direction.x, this.direction.z);
        playerManager.transform.rotation = Quaternion.LookRotation(this.direction);
        playerManager.AnimationManager.PlayAnimation($"ani_spell1a_bat_0815");

        yield return new WaitForSeconds(0.4667f);

        playerManager.MovesController.LaunchMoveForwardProjRpc(direction, damageInfo, distance, "Assets/Prefabs/Objects/Moves/Cinderace/CinderPyroball.prefab");
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideSkillshotAim();
    }

    public override void ResetMove()
    {
        if (moveCoroutine != null)
        {
            playerManager.StopCoroutine(moveCoroutine);
            moveCoroutine = null;
        }
        direction = Vector3.zero;
    }
}
