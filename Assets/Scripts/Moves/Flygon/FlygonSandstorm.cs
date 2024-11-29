using System.Collections;
using UnityEngine;

public class FlygonSandstorm : MoveBase
{
    private float maxRadius = 3f;
    private Vector3 spawnPosition;

    private DamageInfo damageInfo = new DamageInfo(0, 0.55f, 4, 50, DamageType.Physical);

    private string assetPath = "Assets/Prefabs/Objects/Moves/Flygon/SandstormArea.prefab";

    private Coroutine spawnRoutine;

    public FlygonSandstorm()
    {
        Name = "Sandstorm";
        Cooldown = 11f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = playerManager.NetworkObjectId;
        Aim.Instance.InitializeCircleAreaIndicator(maxRadius);
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }

        spawnPosition = Aim.Instance.CircleAreaAim();
    }

    public override void Finish()
    {
        if (IsActive)
        {
            spawnRoutine = playerManager.StartCoroutine(SpawnStorm());

            wasMoveSuccessful = true;
        }
        Aim.Instance.HideCircleAreaIndicator();
        base.Finish();
    }

    private IEnumerator SpawnStorm()
    {
        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            if (obj.TryGetComponent(out SandstormArea area))
            {
                area.InitializeRPC(spawnPosition, damageInfo, playerManager.CurrentTeam.Team);
            }
        };

        playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        playerManager.StopMovementForTime(1f);
        playerManager.transform.LookAt(spawnPosition);
        playerManager.transform.rotation = Quaternion.Euler(0, playerManager.transform.rotation.eulerAngles.y, 0);
        playerManager.AnimationManager.PlayAnimation("Armature_pm0330_00_ba20_buturi01");

        yield return new WaitForSeconds(0.5f);

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

        yield return new WaitForSeconds(0.5f);

        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }

    public override void Cancel()
    {
        Aim.Instance.HideCircleAreaIndicator();
        base.Cancel();
    }

    public override void ResetMove()
    {
        if (spawnRoutine != null)
        {
            playerManager.StopCoroutine(spawnRoutine);
        }
        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }
}
