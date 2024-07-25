using UnityEngine;

public class SylvBabyDollEyes : MoveBase
{
    private DamageInfo damageInfo;
    private float distance;
    private Vector3 direction;

    public SylvBabyDollEyes()
    {
        Name = "Baby-Doll Eyes";
        Cooldown = 5.0f;
        distance = 6f;
        damageInfo = new DamageInfo(0, 0.63f, 18, 330, DamageType.Special);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        Aim.Instance.InitializeSkillshotAim(distance);
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
        if (direction.magnitude != 0 && IsActive)
        {
            Vector2 direction = new Vector2(this.direction.x, this.direction.z);
            playerManager.MovesController.LaunchMoveForwardProjRpc(direction, damageInfo, distance, "Assets/Prefabs/Objects/Moves/Cinderace/CinderPyroball.prefab");
            playerManager.StopMovementForTime(0.25f);
            playerManager.AnimationManager.PlayAnimation($"ani_spell2_bat_0133");
            playerManager.transform.rotation = Quaternion.LookRotation(this.direction);
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSkillshotAim();
        Debug.Log($"Finished {Name}!");
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideSkillshotAim();
    }

    public override void ResetMove()
    {
    }
}
