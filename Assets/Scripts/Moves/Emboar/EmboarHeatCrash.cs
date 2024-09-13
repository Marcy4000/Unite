using DG.Tweening;
using System.Collections;
using UnityEngine;

public class EmboarHeatCrash : MoveBase
{
    private float dashRange = 3f;
    private Vector3 jumpPosition;

    private Coroutine dashCoroutine;
    private EmboarPassive passive;

    private string assetPath = "Assets/Prefabs/Objects/Moves/Emboar/EmboarHeatCrash.prefab";

    private DamageInfo first = new DamageInfo(0, 1.5f, 6, 310, DamageType.Physical, DamageProprieties.CanCrit);
    private DamageInfo second = new DamageInfo(0, 1.86f, 7, 370, DamageType.Physical, DamageProprieties.CanCrit);
    private DamageInfo third = new DamageInfo(0, 2.2f, 8, 450, DamageType.Physical, DamageProprieties.CanCrit);

    public EmboarHeatCrash()
    {
        Name = "Heat Crash";
        Cooldown = 8f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        passive = playerManager.PassiveController.Passive as EmboarPassive;

        first.attackerId = playerManager.NetworkObjectId;
        second.attackerId = playerManager.NetworkObjectId;
        third.attackerId = playerManager.NetworkObjectId;
        Aim.Instance.InitializeCircleAreaIndicator(dashRange);
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }
        jumpPosition = Aim.Instance.CircleAreaAim();
    }

    public override void Finish()
    {
        if (IsActive)
        {
            dashCoroutine = playerManager.StartCoroutine(DoJump());
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideCircleAreaIndicator();
        base.Finish();
    }

    private IEnumerator DoJump()
    {
        playerManager.transform.LookAt(jumpPosition);
        playerManager.transform.rotation = Quaternion.Euler(0, playerManager.transform.rotation.eulerAngles.y, 0);

        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);
        playerManager.PlayerMovement.CanMove = false;
        playerManager.AnimationManager.PlayAnimation("Fight_attack_2");

        yield return new WaitForSeconds(0.316f);

        playerManager.transform.DOJump(jumpPosition, 1.5f, 1, 0.7f);

        yield return new WaitForSeconds(0.7f);

        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            if (obj.TryGetComponent(out EmboarHeatCrashArea heatCrashArea))
            {
                heatCrashArea.InitializeRPC(playerManager.transform.position + (playerManager.transform.forward * 2f), playerManager.transform.forward, first, second, third, passive.IsRecklessActive, IsUpgraded);
            }
        };
        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

        yield return new WaitForSeconds(0.3f);

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        playerManager.PlayerMovement.CanMove = true;
        playerManager.AnimationManager.SetTrigger("Transition");
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideCircleAreaIndicator();
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
