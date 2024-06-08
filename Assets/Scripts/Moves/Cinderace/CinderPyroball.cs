using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CinderPyroball : MoveBase
{
    private DamageInfo damageInfo;
    private float distance;
    private Vector3 direction;

    public CinderPyroball()
    {
        Name = "Pyroball";
        Cooldown = 5.0f;
        distance = 8f;
        damageInfo = new DamageInfo(0, 3.45f, 32, 820, DamageType.Physical);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        Aim.Instance.InitializeSkillshotAimAim(distance);
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
            playerManager.MovesController.LaunchMoveForwardProjRpc(direction, damageInfo, distance, "Moves/CinderPyroball");
            playerManager.StopMovementForTime(1.1f);
            playerManager.AnimationManager.PlayAnimation($"ani_spell1a_bat_0815");
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
}
