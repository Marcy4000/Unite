using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GlaceIcicleSpear : MoveBase
{
    private DamageInfo damageInfo;
    private StatChange selfSlow;
    private float distance;
    private Vector3 direction;

    private string resourcePath = "Moves/Glaceon/IcicleSpear";
    private IcicleSpearHitbox icicleSpearHitbox;

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
        Aim.Instance.InitializeSkillshotAimAim(distance);
        playerManager.MovesController.onObjectSpawned += (icicleSpearHitbox) =>
        {
            this.icicleSpearHitbox = icicleSpearHitbox.GetComponent<IcicleSpearHitbox>();
            this.icicleSpearHitbox.DamageInfo = damageInfo;
            this.icicleSpearHitbox.TeamToIgnore = playerManager.OrangeTeam;
            Debug.Log("Icicle Spear Hitbox spawned!");
        };

        playerManager.MovesController.SpawnNetworkObjectFromStringRPC(resourcePath, playerManager.OwnerClientId);

        playerManager.AnimationManager.PlayAnimation("ani_spell1a1_bat_0471");
        playerManager.Pokemon.AddStatChange(selfSlow);
    }

    public override void Update()
    {
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
        icicleSpearHitbox.transform.rotation = Quaternion.LookRotation(direction);

        Debug.Log($"Icicle Spear Hitbox updated! {icicleSpearHitbox.name}");

        playerManager.transform.rotation = Quaternion.LookRotation(direction);

    }

    public override void Finish()
    {
        wasMoveSuccessful = true;
        playerManager.MovesController.DespawnNetworkObjectRPC(icicleSpearHitbox.GetComponent<NetworkObject>().NetworkObjectId);
        Aim.Instance.HideSkillshotAim();
        icicleSpearHitbox = null;
        playerManager.Pokemon.RemoveStatChangeWithID(3);
        Debug.Log($"Finished {Name}!");
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideSkillshotAim();
    }
}
