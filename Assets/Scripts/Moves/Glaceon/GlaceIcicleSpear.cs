using Unity.Netcode;
using UnityEngine;

public class GlaceIcicleSpear : MoveBase
{
    private DamageInfo damageInfo;
    private StatChange selfSlow;
    private float distance;
    private Vector3 direction;

    private float timer = 0f;

    private string resourcePath = "Assets/Prefabs/Objects/Moves/Glaceon/IcicleSpear.prefab";
    private IcicleSpearHitbox icicleSpearHitbox;

    private GlaceonPassive glaceonPassive;

    private bool activated = false;

    public GlaceIcicleSpear()
    {
        Name = "Icicle Spear";
        Cooldown = 5.0f;
        distance = 9f;
        damageInfo = new DamageInfo(0, 0.64f, 6, 130, DamageType.Special);
        selfSlow = new StatChange(80, Stat.Speed, 0, false, false, true, 3);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);

        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        Aim.Instance.InitializeSkillshotAim(distance);
        playerManager.MovesController.onObjectSpawned += (icicleSpearHitbox) =>
        {
            this.icicleSpearHitbox = icicleSpearHitbox.GetComponent<IcicleSpearHitbox>();
            this.icicleSpearHitbox.InitializeRPC(damageInfo, playerManager.OrangeTeam, IsUpgraded);
            Debug.Log("Icicle Spear Hitbox spawned!");
        };

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(resourcePath, playerManager.OwnerClientId);

        playerManager.AnimationManager.PlayAnimation("ani_spell1a1_bat_0471");
        playerManager.Pokemon.AddStatChange(selfSlow);

        playerManager.MovesController.AddMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.AddMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.AddStatus(ActionStatusType.Busy);

        glaceonPassive = playerManager.PassiveController.Passive as GlaceonPassive;

        BattleUIManager.instance.ShowMoveSecondaryCooldown(0, timer);
        glaceonPassive.ResetTimer();

        if (!activated && glaceonPassive.IciclesCount < 1)
        {
            glaceonPassive.UpdateIciclesCount(2);
            timer = 1f;
        }

        activated = true;
    }

    public override void Update()
    {
        if (!activated)
        {
            return;
        }

        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }

        if (timer <= 0)
        {
            if (glaceonPassive.IciclesCount > 1)
            {
                glaceonPassive.UpdateIciclesCount((byte)Mathf.Clamp(glaceonPassive.IciclesCount - 1, 0, 8));
                glaceonPassive.ResetTimer();
                timer = 1f;
                BattleUIManager.instance.ShowMoveSecondaryCooldown(0, timer);
            }
            else
            {
                glaceonPassive.UpdateIciclesCount((byte)Mathf.Clamp(glaceonPassive.IciclesCount - 1, 0, 8));
                activated = false;
                Finish();
            }
        }

        if (!IsActive)
        {
            return;
        }

        direction = Aim.Instance.SkillshotAim();

        if (icicleSpearHitbox == null)
        {
            Debug.Log("Icicle Spear Hitbox is null!");
            return;
        }

        icicleSpearHitbox.transform.position = playerManager.transform.position;

        if (direction.magnitude != 0)
        {
            icicleSpearHitbox.transform.rotation = Quaternion.LookRotation(direction);
            playerManager.transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    public override void Finish()
    {
        if (!activated)
        {
            wasMoveSuccessful = true;
        }

        if (IsActive)
        {
            playerManager.MovesController.DespawnNetworkObjectRPC(icicleSpearHitbox.GetComponent<NetworkObject>().NetworkObjectId);
            Aim.Instance.HideSkillshotAim();
            icicleSpearHitbox = null;
            playerManager.Pokemon.RemoveStatChangeWithIDRPC(3);
            playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
            playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
            playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
            playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        }
        Debug.Log($"Finished {Name}!");
        base.Finish();
    }

    public override void Cancel()
    {
        if (!IsActive)
        {
            return;
        }
        playerManager.MovesController.DespawnNetworkObjectRPC(icicleSpearHitbox.GetComponent<NetworkObject>().NetworkObjectId);
        Aim.Instance.HideSkillshotAim();
        icicleSpearHitbox = null;
        playerManager.Pokemon.RemoveStatChangeWithIDRPC(3);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);

        base.Cancel();
    }

    public override void ResetMove()
    {
        activated = false;
        if (icicleSpearHitbox != null)
        {
            playerManager.MovesController.DespawnNetworkObjectRPC(icicleSpearHitbox.GetComponent<NetworkObject>().NetworkObjectId);
            icicleSpearHitbox = null;
        }
        playerManager.Pokemon.RemoveStatChangeWithIDRPC(3);
        playerManager.MovesController.RemoveMoveStatus(1, ActionStatusType.Disabled);
        playerManager.MovesController.RemoveMoveStatus(2, ActionStatusType.Disabled);
        playerManager.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }
}
