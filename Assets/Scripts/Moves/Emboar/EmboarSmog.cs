using DG.Tweening;
using System.Collections;
using UnityEngine;

public class EmboarSmog : MoveBase
{
    private float dashRange = 3.5f;
    private Vector3 dashDirection;

    private Coroutine dashCoroutine;

    private string assetPath = "Assets/Prefabs/Objects/Moves/Emboar/EmboarSmogArea.prefab";

    public EmboarSmog()
    {
        Name = "Smog";
        Cooldown = 8f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeDashAim(dashRange);
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }
        dashDirection = Aim.Instance.DashAim();
    }

    public override void Finish()
    {
        if (IsActive)
        {
            dashCoroutine = playerManager.StartCoroutine(DoDash());
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideDashAim();
        base.Finish();
    }

    private IEnumerator DoDash()
    {
        playerManager.transform.LookAt(playerManager.transform.position - dashDirection);
        playerManager.transform.rotation = Quaternion.Euler(0, playerManager.transform.rotation.eulerAngles.y, 0);

        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);
        playerManager.PlayerMovement.CanMove = false;
        playerManager.AnimationManager.PlayAnimation("Pet_happy");

        Vector3 spawnPos = playerManager.transform.position - (dashDirection * 0.5f);

        playerManager.transform.DOJump(playerManager.transform.position + (dashDirection * dashRange), 1f, 1, 0.5f);

        yield return new WaitForSeconds(0.05f);

        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            if (obj.TryGetComponent(out EmboarSmogArea smogArea))
            {
                smogArea.InitializeRPC(spawnPos, 3f, playerManager.CurrentTeam.Team);
            }
        };
        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

        yield return new WaitForSeconds(0.45f);

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        playerManager.PlayerMovement.CanMove = true;
        playerManager.AnimationManager.SetTrigger("Transition");
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideDashAim();
    }

    public override void ResetMove()
    {
        if (dashCoroutine != null)
        {
            playerManager.StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }
    }
}
