using DG.Tweening;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MarshadowShadowSneak : MoveBase
{
    // This ability raises the movement speed of the user while allowing the user to permeate through terrain.

    private bool isUnderwater;
    private bool isFinishing;
    private float underwaterTime = 3.5f;

    private GameObject shadowSneakWarning;
    private string assetPath = "Assets/Prefabs/Objects/Moves/Marshadow/MarshadowShadowSneak.prefab";

    private StatusEffect underwaterEffect = new StatusEffect(StatusType.Invincible, 0, false, 1);
    private StatChange speedBoost = new StatChange(20, Stat.Speed, 0f, false, true, true, 17);

    public MarshadowShadowSneak()
    {
        Name = "Shadow Sneak";
        Cooldown = 6f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
    }

    public override void Update()
    {
        if (isUnderwater)
        {
            if (shadowSneakWarning != null)
            {
                shadowSneakWarning.transform.position = new Vector3(playerManager.transform.position.x, 1.5f, playerManager.transform.position.z);
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
                underwaterTime = 3.5f;
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
        playerManager.AnimationManager.PlayAnimation("pm0883_ba01_landA01");
        playerManager.PlayerMovement.CanMove = false;
        playerManager.transform.DOJump(playerManager.transform.position + playerManager.transform.up * -2.6f, 3, 1, 0.5f);

        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            shadowSneakWarning = obj;
        };

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath, playerManager.OwnerClientId);

        yield return new WaitForSeconds(0.5f);

        playerManager.PlayerMovement.IsFlying = true;
        playerManager.Pokemon.AddStatChange(speedBoost);
        playerManager.PlayerMovement.CanMove = true;
        playerManager.AnimationManager.SetTrigger("Transition");
    }

    private IEnumerator JumpOutWater()
    {
        playerManager.AnimationManager.PlayAnimation("pm0883_ba01_landA01");
        playerManager.PlayerMovement.CanMove = false;
        playerManager.transform.DOJump(playerManager.transform.position + playerManager.transform.up * 2.6f, 3, 1, 0.5f);
        playerManager.Pokemon.RemoveStatChangeWithIDRPC(speedBoost.ID);
        
        playerManager.MovesController.DespawnNetworkObjectRPC(shadowSneakWarning.GetComponent<NetworkObject>().NetworkObjectId);
        shadowSneakWarning = null;

        yield return new WaitForSeconds(0.3f);

        playerManager.AnimationManager.PlayAnimation("pm0883_ba01_landC01");

        yield return new WaitForSeconds(0.2f);
        playerManager.PlayerMovement.CanMove = true;
        playerManager.PlayerMovement.IsFlying = false;

        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

        isUnderwater = false;
    }

    public override void ResetMove()
    {
        if (shadowSneakWarning != null)
        {
            playerManager.MovesController.DespawnNetworkObjectRPC(shadowSneakWarning.GetComponent<NetworkObject>().NetworkObjectId);
            shadowSneakWarning = null;
        }
        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        playerManager.Pokemon.RemoveStatusEffectWithID(underwaterEffect.ID);
        playerManager.PlayerMovement.IsFlying = false;
        isUnderwater = false;
        isFinishing = false;
        underwaterTime = 5f;
        playerManager.transform.DOKill();
    }
}
