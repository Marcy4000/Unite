using System.Collections;
using UnityEngine;

public class VaporeonUnite : MoveBase
{
    private DamageInfo damageInfo = new DamageInfo(0, 0.92f, 9, 640, DamageType.Special);
    private DamageInfo healInfo = new DamageInfo(0, 2.6f, 5, 840, DamageType.Special);

    private string assetPath = "Assets/Prefabs/Objects/Moves/Vaporeon/VaporeonUnite.prefab";

    private VaporUniteArea area;

    private Coroutine uniteRoutine;

    public VaporeonUnite()
    {
        Name = "Turbo Tempest";
        Cooldown = 0;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = playerManager.NetworkObjectId;
        healInfo.attackerId = playerManager.NetworkObjectId;
        Aim.Instance.InitializeSimpleCircle(6);
    }

    public override void Update()
    {
        
    }

    public override void Finish()
    {
        if (IsActive)
        {
            uniteRoutine = playerManager.StartCoroutine(CastUnite());
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSimpleCircle();
        base.Finish();
    }

    public override void Cancel()
    {
        Aim.Instance.HideSimpleCircle();
        base.Cancel();
    }

    private IEnumerator CastUnite()
    {
        playerManager.AnimationManager.PlayAnimation("Armature_pm0134_00_kw32_happyC01_gfbanm");
        playerManager.PlayerMovement.CanMove = false;
        playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            area = obj.GetComponent<VaporUniteArea>();
            area.InitializeRPC(damageInfo, healInfo, playerManager.OrangeTeam, new Vector3(playerManager.transform.position.x, 1.5f, playerManager.transform.position.z));
        };

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

        yield return new WaitForSeconds(0.35f);

        area.DoExplosionRPC();

        yield return new WaitForSeconds(0.95f);
        playerManager.PlayerMovement.CanMove = true;
        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }

    public override void ResetMove()
    {
        if (uniteRoutine != null)
        {
            playerManager.StopCoroutine(uniteRoutine);
        }
        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }
}
