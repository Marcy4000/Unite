using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoltSwift : MoveBase
{
    private DamageInfo bigStar = new DamageInfo(0, 1.2f, 8, 220, DamageType.Physical);
    private DamageInfo smallStar = new DamageInfo(0, 1f, 6, 180, DamageType.Physical);

    private string assetPath = "Assets/Prefabs/Objects/Moves/Jolteon/JoltSwift.prefab";
    private float range = 5f;

    private Vector3 direction;

    public JoltSwift()
    {
        Name = "Swift";
        Cooldown = 7f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        bigStar.attackerId = playerManager.NetworkObjectId;
        smallStar.attackerId = playerManager.NetworkObjectId;

        Aim.Instance.InitializeSkillshotAimAim(range);
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }

        direction = Aim.Instance.SkillshotAim();
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                JoltSwiftProjectile projectile = obj.GetComponent<JoltSwiftProjectile>();
                Vector3 startPos = playerManager.transform.position + new Vector3(0, 1f, 0);
                projectile.InitializeRPC(startPos, direction, true, bigStar, smallStar, playerManager.OrangeTeam);
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);
            playerManager.AnimationManager.PlayAnimation($"ani_leafeonspell1_bat_0133_1");
            playerManager.transform.rotation = Quaternion.LookRotation(direction);
            playerManager.StopMovementForTime(0.3f);

            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSkillshotAim();
        base.Finish();
    }

    public override void Cancel()
    {
        Aim.Instance.HideSkillshotAim();
        base.Cancel();
    }
}
