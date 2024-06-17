using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VaporDive : MoveBase
{
    private bool isUnderwater;
    private bool isFinishing;
    private float underwaterTime = 5f;

    private DamageInfo damageInfo = new DamageInfo(0, 0.25f, 6, 120, DamageType.Special);

    private VaporeonDiveWarning diveWarning;
    private string assetPath = "Moves/Vaporeon/VaporeonDive";

    private StatusEffect underwaterEffect = new StatusEffect(StatusType.Invincible, 0, false, 1);

    public VaporDive()
    {
        Name = "Dive";
        Cooldown = 10f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = playerManager.NetworkObjectId;
    }

    public override void Update()
    {
        if (isUnderwater)
        {
            if (diveWarning != null)
            {
                diveWarning.transform.position = new Vector3(playerManager.transform.position.x, 1.5f, playerManager.transform.position.z);
            }

            if (underwaterTime >= 0)
            {
                underwaterTime -= Time.deltaTime;
            }

            if (underwaterTime <= 0 && !isFinishing)
            {
                IsActive = true;
                Finish();
                isFinishing = true;
            }
        }
    }

    public override void Finish()
    {
        if (IsActive)
        {
            if (!isUnderwater)
            {
                playerManager.Pokemon.AddStatusEffect(underwaterEffect);
                playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Disabled);
                playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
                playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
                playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);
                playerManager.StartCoroutine(JumpInWater());
                underwaterTime = 6f;
                BattleUIManager.instance.ShowMoveSecondaryCooldown(1, underwaterTime);
                isFinishing = false;
            }
            else
            {
                isFinishing = true;
                playerManager.StartCoroutine(JumpOutWater());
                playerManager.Pokemon.RemoveStatusEffectWithID(underwaterEffect.ID);
                wasMoveSuccessful = true;
                BattleUIManager.instance.ShowMoveSecondaryCooldown(1, 0);
            }
        }
        base.Finish();
    }

    private IEnumerator JumpInWater()
    {
        isUnderwater = true;
        playerManager.AnimationManager.PlayAnimation("Armature_pm0134_00_ba01_landA01_gfbanm");
        playerManager.PlayerMovement.CanMove = false;
        playerManager.transform.DOJump(playerManager.transform.position + playerManager.transform.up * -2.6f, 3, 1, 0.8f);

        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            if (obj.TryGetComponent(out VaporeonDiveWarning warning))
            {
                diveWarning = warning;
                diveWarning.InitializeRPC(playerManager.transform.position, playerManager.OrangeTeam, damageInfo);
            }
        };

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath, playerManager.OwnerClientId);

        yield return new WaitForSeconds(0.8f);
        playerManager.PlayerMovement.CanMove = true;
        playerManager.AnimationManager.SetTrigger("Transition");
    }

    private IEnumerator JumpOutWater()
    {
        playerManager.AnimationManager.PlayAnimation("Armature_pm0134_00_ba01_landA01_gfbanm");
        playerManager.PlayerMovement.CanMove = false;
        playerManager.transform.DOJump(playerManager.transform.position + playerManager.transform.up * 2.6f, 3, 1, 0.8f);
        yield return new WaitForSeconds(0.5f);

        playerManager.AnimationManager.PlayAnimation("Armature_pm0134_00_ba01_landC01_gfbanm");
        if (diveWarning != null)
        {
            diveWarning.DoPushbackRPC();
        }

        yield return new WaitForSeconds(0.3f);
        playerManager.PlayerMovement.CanMove = true;

        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

        diveWarning = null;

        isUnderwater = false;
    }
}
