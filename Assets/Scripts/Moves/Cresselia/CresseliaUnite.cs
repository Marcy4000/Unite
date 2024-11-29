using DG.Tweening;
using UnityEngine;

public class CresseliaUnite : MoveBase
{
    private StatusEffect invincible = new StatusEffect(StatusType.Invincible, 0f, false, 10);
    private DamageInfo damage = new DamageInfo(0, 0.8f, 7, 740, DamageType.Special);

    private const float maxUseCooldown = 5f;

    private bool isInAir;
    private float useCooldown;

    private CresseliaUniteArea uniteArea;
    private string assetPath = "Assets/Prefabs/Objects/Moves/Cresselia/CresseliaUniteWarning.prefab";

    public CresseliaUnite()
    {
        Name = "Crescent Crash";
        Cooldown = 0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeSimpleCircle(5.5f);
        damage.attackerId = playerManager.NetworkObjectId;
    }

    public override void Update()
    {
        if (isInAir)
        {
            useCooldown -= Time.deltaTime;

            if (useCooldown <= 0)
            {
                IsActive = true;
                Finish();
            }
        }

        if (uniteArea != null)
        {
            Vector3 position = playerManager.transform.position;
            position.y = 0.11f;
            uniteArea.transform.position = position;
        }

        if (!IsActive)
        {
            return;
        }
    }

    public override void Finish()
    {
        if (IsActive)
        {
            if (!isInAir)
            {
                useCooldown = maxUseCooldown;
                isInAir = true;

                playerManager.MovesController.onObjectSpawned += (obj) =>
                {
                    uniteArea = obj.GetComponent<CresseliaUniteArea>();
                    uniteArea.InitializeRPC(damage, playerManager.CurrentTeam.Team);
                };
                playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath, playerManager.OwnerClientId);

                playerManager.PlayerMovement.SnapToGround = false;
                playerManager.PlayerMovement.CanMove = false;
                playerManager.MovesController.LockEveryAction();
                playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);
                playerManager.transform.DOMoveY(3f, 0.2f).onComplete += () =>
                {
                    playerManager.PlayerMovement.CanMove = true;
                    playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
                };

                playerManager.Pokemon.AddStatusEffect(invincible);
                BattleUIManager.instance.ShowUniteMoveSecondaryCooldown(maxUseCooldown);
            }
            else
            {
                isInAir = false;
                BattleUIManager.instance.ShowUniteMoveSecondaryCooldown(0);
                
                playerManager.transform.DOJump(new Vector3(playerManager.transform.position.x, 0, playerManager.transform.position.z), 3f, 1, 0.3f).onComplete += () =>
                {
                    playerManager.PlayerMovement.CanMove = true;
                    playerManager.PlayerMovement.SnapToGround = true;
                    playerManager.MovesController.UnlockEveryAction();
                    playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
                };

                uniteArea.DoDamageRPC();
                playerManager.Pokemon.RemoveStatusEffectWithID(invincible.ID);
                wasMoveSuccessful = true;
            }
        }
        Aim.Instance.HideSimpleCircle();
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideSimpleCircle();
    }

    public override void ResetMove()
    {
        playerManager.PlayerMovement.SnapToGround = true;
        isInAir = false;
        useCooldown = 0;
        if (uniteArea != null)
        {
            uniteArea.DespawnRPC();
        }
    }
}
