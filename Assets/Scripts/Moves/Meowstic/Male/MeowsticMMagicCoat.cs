using System.Collections;
using UnityEngine;

public class MeowsticMMagicCoat : MoveBase
{
    private string assetPath = "Assets/Prefabs/Objects/Moves/Meowstic/Male/MeowsticMMagicCoat.prefab";

    public MeowsticMMagicCoat()
    {
        Name = "Magic Coat";
        Cooldown = 9f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeSimpleCircle(3f);
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.StopMovementForTime(0.6f);
            playerManager.StartCoroutine(StopActionsForTime());
            playerManager.AnimationManager.PlayAnimation("Armature_pm0734_00_kw30_hate01");
            playerManager.Pokemon.AddStatChange(new StatChange(20, Stat.Speed, 4f, true, true, false, 0));
            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                MeowsticMMagicCoatArea shield = obj.GetComponent<MeowsticMMagicCoatArea>();
                shield.InitializeRPC(playerManager.NetworkObjectId);
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSimpleCircle();
        base.Finish();
    }

    private IEnumerator StopActionsForTime()
    {
        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        yield return new WaitForSeconds(0.61f);

        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }

    public override void Cancel()
    {
        Aim.Instance.HideSimpleCircle();
        base.Cancel();
    }

    public override void ResetMove()
    {
        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }
}
