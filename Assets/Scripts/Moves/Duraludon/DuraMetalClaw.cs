using UnityEngine;

public class DuraMetalClaw : MoveBase
{
    private float range = 7f;
    private DamageInfo damageInfo = new DamageInfo(0, 0.94f, 8, 260, DamageType.Physical);

    private string assetPath = "Assets/Prefabs/Objects/Moves/Duraludon/DuraMetalClawProjectile.prefab";

    private DuraBasicAtk duraBasicAtk;

    private Vector3 direction;
    private bool initialized = false;

    public DuraMetalClaw()
    {
        Name = "Metal Claw";
        Cooldown = 8f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);

        if (!initialized)
        {
            duraBasicAtk = playerManager.MovesController.BasicAttack as DuraBasicAtk;
            initialized = true;
        }

        Aim.Instance.InitializeSkillshotAim(range);
        damageInfo.attackerId = playerManager.NetworkObjectId;
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
                if (obj.TryGetComponent(out DuraMetalClawProjectile projectile))
                {
                    projectile.InitializeRPC(direction, playerManager.transform.position, playerManager.CurrentTeam.Team, damageInfo);
                }
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

            playerManager.transform.rotation = Quaternion.LookRotation(direction);

            string animationName = duraBasicAtk.IsInCannonMode ? "ani_spell1a2b_bat_0884" : "ani_spell2_bat_0884";
            playerManager.AnimationManager.PlayAnimation(animationName);

            if (!duraBasicAtk.IsInCannonMode)
                playerManager.StopMovementForTime(0.35f, true);

            duraBasicAtk.GainBoostedAttack();

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
    }
}
