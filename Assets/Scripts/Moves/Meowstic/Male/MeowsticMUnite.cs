using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeowsticMUnite : MoveBase
{
    private string assetPath = "Assets/Prefabs/Objects/Moves/Meowstic/Male/MeowsticMUniteArea.prefab";

    public MeowsticMUnite()
    {
        Name = "Mystic Harmony";
        Cooldown = 0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeSimpleCircle(5.5f);
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
            playerManager.StartCoroutine(FlyState());
            
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSimpleCircle();
        base.Finish();
    }

    private IEnumerator FlyState()
    {
        playerManager.MovesController.LockEveryAction();
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        playerManager.StopMovementForTime(0.7f, false);
        playerManager.AnimationManager.SetTrigger("Unite");
        playerManager.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, 6f, true, 0));
        playerManager.Pokemon.AddStatChange(new StatChange(20, Stat.Speed, 6f, true, true, false, 0));

        yield return new WaitForSeconds(0.6f);

        playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                MeowsticMUniteArea area = obj.GetComponent<MeowsticMUniteArea>();
                area.InitializeRPC(playerManager.NetworkObjectId);
            };

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

        yield return new WaitForSeconds(6f);

        playerManager.AnimationManager.SetTrigger("Transition");
        playerManager.StopMovementForTime(0.3f, false);
        playerManager.MovesController.UnlockEveryAction();
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

    }

    public override void Cancel()
    {
        Aim.Instance.HideSimpleCircle();
        base.Cancel();
    }
}
