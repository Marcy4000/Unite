using System.Collections;
using UnityEngine;

public class MeowsticMWonderRoom : MoveBase
{
    private float maxRadius = 3.5f;
    private Vector3 spawnPosition;

    private DamageInfo damageInfo = new DamageInfo(0, 0.55f, 4, 50, DamageType.Physical);

    private string assetPath = "Assets/Prefabs/Objects/Moves/Meowstic/Male/MeowsticMWonderRoom.prefab";

    public MeowsticMWonderRoom()
    {
        Name = "Wonder Room";
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
            playerManager.StartCoroutine(SpawnWonderRoom());

            wasMoveSuccessful = true;
        }
        Aim.Instance.HideCircleAreaIndicator();
        base.Finish();
    }

    private IEnumerator SpawnWonderRoom()
    {
        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            if (obj.TryGetComponent(out MeowsticMWonderRoomArea area))
            {
                area.InitializeRPC(spawnPosition, playerManager.transform.position, playerManager.CurrentTeam.Team);
            }
        };

        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        playerManager.StopMovementForTime(0.75f);
        playerManager.transform.LookAt(spawnPosition);
        playerManager.transform.rotation = Quaternion.Euler(0, playerManager.transform.rotation.eulerAngles.y, 0);
        playerManager.AnimationManager.PlayAnimation("Armature_pm0734_00_ba21_tokusyu01");

        yield return new WaitForSeconds(0.5f);

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

        yield return new WaitForSeconds(0.26f);

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }

    public override void Cancel()
    {
        Aim.Instance.HideCircleAreaIndicator();
        base.Cancel();
    }

    public override void ResetMove()
    {
        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }
}
