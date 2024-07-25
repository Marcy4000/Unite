using DG.Tweening;
using System.Collections;
using UnityEngine;

public class FlygonDig : MoveBase
{
    // Totally not just vaporeon dive copy pasted

    private bool isUnderground;
    private bool isFinishing;
    private float underwaterTime = 4f;

    private DamageInfo damageInfo = new DamageInfo(0, 0.6f, 6, 140, DamageType.Physical);

    private FlygonDigWarning diveWarning;
    private string assetPath = "Assets/Prefabs/Objects/Moves/Flygon/FlygonDig.prefab";

    private StatusEffect underwaterEffect = new StatusEffect(StatusType.Invincible, 0, false, 1);
    private StatChange slow = new StatChange(40, Stat.Speed, 0, false, false, true, 11);

    public FlygonDig()
    {
        Name = "Dig";
        Cooldown = 8f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = playerManager.NetworkObjectId;
        Aim.Instance.InitializeSimpleCircle(1.5f);
    }

    public override void Update()
    {
        if (isUnderground)
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
            if (!isUnderground)
            {
                playerManager.Pokemon.AddStatusEffect(underwaterEffect);
                playerManager.MovesController.AddMoveStatus(0, ActionStatusType.Disabled);
                playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
                playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
                playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);
                playerManager.StartCoroutine(JumpInWater());
                underwaterTime = 4f;
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
        Aim.Instance.HideSimpleCircle();
        base.Finish();
    }

    public override void Cancel()
    {
        Aim.Instance.HideSimpleCircle();
        base.Cancel();
    }

    private IEnumerator JumpInWater()
    {
        isUnderground = true;
        playerManager.AnimationManager.PlayAnimation("Armature_pm0328_ba41_down01");
        playerManager.PlayerMovement.CanMove = false;
        playerManager.transform.DOJump(playerManager.transform.position + playerManager.transform.up * -2.6f, 1, 1, 0.8f);

        playerManager.MovesController.onObjectSpawned += (obj) =>
        {
            if (obj.TryGetComponent(out FlygonDigWarning warning))
            {
                diveWarning = warning;
                diveWarning.InitializeRPC(playerManager.transform.position, playerManager.OrangeTeam, damageInfo);
            }
        };

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath, playerManager.OwnerClientId);

        yield return new WaitForSeconds(0.8f);
        playerManager.Pokemon.AddStatChange(slow);
        playerManager.PlayerMovement.CanMove = true;
        playerManager.AnimationManager.SetTrigger("Transition");
    }

    private IEnumerator JumpOutWater()
    {
        playerManager.PlayerMovement.CanMove = false;
        playerManager.transform.DOJump(playerManager.transform.position + playerManager.transform.up * 2.6f, 1, 1, 0.8f);
        yield return new WaitForSeconds(0.3f);

        if (diveWarning != null)
        {
            diveWarning.GiveDamageRPC();
        }

        yield return new WaitForSeconds(0.5f);
        playerManager.PlayerMovement.CanMove = true;
        playerManager.Pokemon.RemoveStatChangeWithIDRPC(slow.ID);

        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

        diveWarning = null;

        isUnderground = false;
    }

    public override void ResetMove()
    {
        if (diveWarning != null)
        {
            diveWarning.DespawnRPC();
        }
        playerManager.Pokemon.RemoveStatChangeWithIDRPC(slow.ID);
        playerManager.MovesController.RemoveMoveStatus(0, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        isUnderground = false;
        playerManager.transform.DOKill();
    }
}
