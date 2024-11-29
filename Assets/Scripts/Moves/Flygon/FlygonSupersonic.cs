using UnityEngine;

public class FlygonSupersonic : MoveBase
{
    private float range = 7f;
    private Vector3 direction;

    private string assetPath = "Assets/Prefabs/Objects/Moves/Flygon/FlygonSupersonic.prefab";
    private SupersonicHitbox hitbox;

    public FlygonSupersonic()
    {
        Name = "Supersonic";
        Cooldown = 7f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeSkillshotAim(range);
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
                if (obj.TryGetComponent(out SupersonicHitbox hitbox))
                {
                    this.hitbox = hitbox;
                    hitbox.InitializeRPC(playerManager.transform.position, direction, playerManager.CurrentTeam.Team);
                }
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

            playerManager.StopMovementForTime(1.2f);
            playerManager.transform.rotation = Quaternion.LookRotation(direction);
            string animation = playerManager.Pokemon.CurrentLevel >= 6 ? "Armature_pm0330_00_ba21_tokusyu01" : "Armature_pm0329_00_ba21_tokusyu01";
            playerManager.AnimationManager.PlayAnimation(animation);

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

    public override void ResetMove()
    {
        if (hitbox != null)
        {
            playerManager.MovesController.DespawnNetworkObjectRPC(hitbox.NetworkObjectId);
        }
    }
}
