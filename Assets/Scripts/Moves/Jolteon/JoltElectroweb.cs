using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoltElectroweb : MoveBase
{
    private float range = 7f;
    private DamageInfo tickDamage = new DamageInfo(0, 0.6f, 4, 60, DamageType.Physical);

    private string assetPath = "Assets/Prefabs/Objects/Moves/Jolteon/ElectrowebProjectile.prefab";

    private Vector3 direction;

    public JoltElectroweb()
    {
        Name = "Electroweb";
        Cooldown = 10.0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeSkillshotAimAim(range);
        tickDamage.attackerId = playerManager.NetworkObjectId;
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
        if (IsActive && direction.magnitude != 0)
        {
            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                if (obj.TryGetComponent(out ElectroWebProjectile projectile))
                {
                    projectile.InitializeRPC(direction, playerManager.transform.position, playerManager.OrangeTeam, tickDamage);
                }
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

            playerManager.transform.rotation = Quaternion.LookRotation(direction);
            playerManager.StopMovementForTime(0.3f, false);

            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSkillshotAim();
        base.Finish();
    }
}
