using System.Collections;
using UnityEngine;

public class FlareonHeatWave : MoveBase
{
    private DamageInfo damage = new DamageInfo(0, 1.25f, 8, 200, DamageType.Physical, DamageProprieties.CanCrit);
    private DamageInfo burnedDamage = new DamageInfo(0, 1.3f, 9, 250, DamageType.Physical, DamageProprieties.CanCrit);
    private float range = 5f;

    private string assetPath = "Assets/Prefabs/Objects/Moves/Flareon/FlareonHeatWave.prefab";
    private StatChange speedBoost = new StatChange(20, Stat.Speed, 6f, true, true, true, 0);

    public FlareonHeatWave()
    {
        Name = "Heat Wave";
        Cooldown = 9f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeSimpleCircle(range);
        damage.attackerId = playerManager.NetworkObjectId;
        burnedDamage.attackerId = playerManager.NetworkObjectId;
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
                if (obj.TryGetComponent(out FlareonHeatWaveArea hitbox))
                {
                    hitbox.InitializeRPC(playerManager.NetworkObjectId, playerManager.CurrentTeam.Team, damage, burnedDamage, IsUpgraded);
                }
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);
            playerManager.StopMovementForTime(0.6f);
            playerManager.AnimationManager.PlayAnimation("pm0136_kw35_playA01");
            playerManager.StartCoroutine(StopControls());

            playerManager.Pokemon.AddStatChange(speedBoost);

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

        yield return new WaitForSeconds(0.6f);

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
