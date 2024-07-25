using System.Collections;
using UnityEngine;
using DG.Tweening;

public class VaporRainDance : MoveBase
{
    private Vector3 destination;

    private string assetPath = "Assets/Prefabs/Objects/Moves/Vaporeon/VaporeonRain.prefab";
    private DamageInfo heal = new DamageInfo(0, 0.38f, 7, 300, DamageType.Special);

    private float range = 4f;

    public VaporRainDance()
    {
        Name = "Rain Dance";
        Cooldown = 12.5f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeCircleAreaIndicator(range);
        heal.attackerId = playerManager.NetworkObjectId;
        Debug.Log($"Executed {Name}!");
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }
        destination = Aim.Instance.CircleAreaAim();
    }

    public override void Finish()
    {
        if (IsActive)
        {
            JumpToLocation();
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideCircleAreaIndicator();
        base.Finish();
    }

    private void JumpToLocation()
    {
        playerManager.PlayerMovement.CanMove = false;
        playerManager.AnimationManager.PlayAnimation($"Armature_pm0134_00_kw35_playA01_gfbanm");
        playerManager.transform.LookAt(destination);
        playerManager.transform.rotation = Quaternion.Euler(0, playerManager.transform.rotation.eulerAngles.y, 0);

        playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
        playerManager.MovesController.BattleItemStatus.AddStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        var jump = playerManager.transform.DOJump(destination, 1f, 1, 0.35f);
        jump.onComplete += () =>
        {
            playerManager.PlayerMovement.CanMove = true;
            playerManager.AnimationManager.SetTrigger("Transition");
            playerManager.StartCoroutine(SummonRain());
        };
    }

    private IEnumerator SummonRain()
    {
        playerManager.PlayerMovement.CanMove = false;
        playerManager.AnimationManager.PlayAnimation("Armature_pm0134_00_kw33_moveC01_gfbanm");

        yield return new WaitForSeconds(0.35f);

        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            if (obj.TryGetComponent(out VaporeonRain rain))
            {
                rain.InitializeRPC(destination, playerManager.OrangeTeam, heal);
            }
        };

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

        yield return new WaitForSeconds(0.85f);

        playerManager.PlayerMovement.CanMove = true;
        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.MovesController.BattleItemStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideCircleAreaIndicator();
    }

    override public void ResetMove()
    {
        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        playerManager.DOKill();
    }
}
