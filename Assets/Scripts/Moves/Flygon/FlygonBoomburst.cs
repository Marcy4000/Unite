using System.Collections;
using UnityEngine;

public class FlygonBoomburst : MoveBase
{
    private DamageInfo closeDamage = new DamageInfo(0, 1.2f, 8, 200, DamageType.Physical);
    private DamageInfo farDamage = new DamageInfo(0, 1f, 7, 150, DamageType.Physical);
    private float range = 5f;

    private string assetPath = "Assets/Prefabs/Objects/Moves/Flygon/FlygonBoomburst.prefab";

    public FlygonBoomburst()
    {
        Name = "Boomburst";
        Cooldown = 7.5f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeSimpleCircle(range);
        closeDamage.attackerId = playerManager.NetworkObjectId;
        farDamage.attackerId = playerManager.NetworkObjectId;
    }

    public override void Update()
    {

    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                if (obj.TryGetComponent(out BoomburstHitbox hitbox))
                {
                    hitbox.InitializeRPC(playerManager.transform.position, playerManager.CurrentTeam.Team, closeDamage, farDamage);
                }
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);
            playerManager.StopMovementForTime(1.5f);
            string animationName = playerManager.Pokemon.CurrentLevel >= 6 ? "Armature_pm0330_00_ba02_roar01" : "Armature_pm0329_00_ba02_roar01";
            playerManager.AnimationManager.PlayAnimation(animationName);
            playerManager.StartCoroutine(StopControls());

            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSimpleCircle();
        base.Finish();
    }

    private IEnumerator StopControls()
    {
        playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        yield return new WaitForSeconds(1.5f);

        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }

    public override void Cancel()
    {
        Aim.Instance.HideSimpleCircle();
        base.Cancel();
    }

    public override void ResetMove()
    {
        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }
}
