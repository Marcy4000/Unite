using UnityEngine;

public class VaporSwift : MoveBase
{
    private DamageInfo damageInfo;
    private float distance;
    private Vector3 position;

    private string attackPrefab = "Assets/Prefabs/Objects/Moves/Vaporeon/VaporeonSwift.prefab";

    public VaporSwift()
    {
        Name = "Swift";
        Cooldown = 7.5f;
        distance = 6f;
        damageInfo = new DamageInfo(0, 0.41f, 5, 170, DamageType.Special);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        Aim.Instance.InitializeCircleAreaIndicator(distance);
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }
        position = Aim.Instance.CircleAreaAim();
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.MovesController.onObjectSpawned += (spawnedObject) =>
            {
                if (spawnedObject.TryGetComponent(out VaporSwiftProjectile projectile))
                {
                    projectile.InitializeRPC(damageInfo, playerManager.OrangeTeam, position);
                }
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(attackPrefab);
            playerManager.transform.LookAt(position);
            playerManager.transform.rotation = Quaternion.Euler(0, playerManager.transform.rotation.eulerAngles.y, 0);
            playerManager.AnimationManager.PlayAnimation("ani_leafeonatk3_bat_0133");
            playerManager.StopMovementForTime(0.35f);

            wasMoveSuccessful = true;
        }
        Aim.Instance.HideCircleAreaIndicator();
        Debug.Log($"Finished {Name}!");
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideCircleAreaIndicator();
    }
}
