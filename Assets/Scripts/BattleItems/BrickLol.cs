using UnityEngine;

public class BrickLol : BattleItemBase
{
    private float distance = 5f;
    private Vector3 landDestination;

    private DamageInfo damage = new DamageInfo(0, 0f, 0, 400, DamageType.Physical, DamageProprieties.CanCrit);
    private string brickKey = "Assets/Prefabs/Objects/Objects/Brick.prefab";

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        wasUseSuccessful = false;

        damage.attackerId = playerManager.NetworkObjectId;

        Aim.Instance.InitializeCircleAreaIndicator(distance);
        Cooldown = 55f;
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }

        landDestination = Aim.Instance.CircleAreaAim();
    }

    public override void Finish()
    {
        if (IsActive)
        {
            wasUseSuccessful = true;
            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                if (obj.TryGetComponent(out BrickObject brick))
                {
                    brick.InitializeRPC(playerManager.transform.position, landDestination, playerManager.CurrentTeam.Team, damage);
                }
            };
            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(brickKey);
        }
        Aim.Instance.HideCircleAreaIndicator();
        base.Finish();
    }

    public override void Cancel()
    {
        Aim.Instance.HideCircleAreaIndicator();
        base.Cancel();
    }
}
