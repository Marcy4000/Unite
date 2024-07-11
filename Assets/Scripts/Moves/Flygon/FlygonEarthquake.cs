using DG.Tweening;
using System.Collections;
using UnityEngine;

public class FlygonEarthquake : MoveBase
{
    private Vector3 spawnPosition;
    private float range = 3.5f;

    private DamageInfo first = new DamageInfo(0, 1.1f, 6, 110, DamageType.Physical);
    private DamageInfo second = new DamageInfo(0, 1.25f, 7, 125, DamageType.Physical);
    private DamageInfo third = new DamageInfo(0, 1.4f, 8, 140, DamageType.Physical);

    private string assetPath = "Assets/Prefabs/Objects/Moves/Flygon/EarthquakeObject.prefab";

    public FlygonEarthquake()
    {
        Name = "Earthquake";
        Cooldown = 9.5f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        first.attackerId = playerManager.NetworkObjectId;
        second.attackerId = playerManager.NetworkObjectId;
        third.attackerId = playerManager.NetworkObjectId;
        Aim.Instance.InitializeCircleAreaIndicator(range);
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
            playerManager.StartCoroutine(SpawnEarthquake());
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideCircleAreaIndicator();
        base.Finish();
    }

    private IEnumerator SpawnEarthquake()
    {
        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            if (obj.TryGetComponent(out EarthquakeArea area))
            {
                area.InitializeRPC(spawnPosition, first, second, third, playerManager.OrangeTeam);
            }
        };

        playerManager.PlayerMovement.CanMove = false;
        playerManager.transform.LookAt(spawnPosition);
        playerManager.transform.rotation = Quaternion.Euler(0, playerManager.transform.rotation.eulerAngles.y, 0);

        playerManager.transform.DOJump(spawnPosition, 2.5f, 1, 0.65f).onComplete += () =>
        {
            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

            playerManager.AnimationManager.PlayAnimation("Armature_pm0330_00_cm10_kwwait_bawait01");
            playerManager.StopMovementForTime(0.6f, false);
        };
        playerManager.AnimationManager.PlayAnimation("Armature_pm0330_00_cm10_bawait_kwwait01");

        playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        yield return new WaitForSeconds(0.7f);

        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }
}
