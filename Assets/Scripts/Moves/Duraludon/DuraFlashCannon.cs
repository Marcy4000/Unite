using UnityEngine;

public class DuraFlashCannon : MoveBase
{
    private float range = 11.5f;
    private DamageInfo damageInfo = new DamageInfo(0, 0.52f, 2, 60, DamageType.Physical);

    private string assetPath = "Assets/Prefabs/Objects/Moves/Duraludon/DuraFlashCannonObject.prefab";

    private DuraBasicAtk duraBasicAtk;
    private DuraFlashCannonObj flashCannonInstance;

    private Vector3 direction;
    private bool initialized = false;

    private bool isInCannonMode = false;

    public DuraFlashCannon()
    {
        Name = "Flash Cannon";
        Cooldown = 5f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);

        if (!initialized)
        {
            duraBasicAtk = playerManager.MovesController.BasicAttack as DuraBasicAtk;
            duraBasicAtk.onExitCannonMode += OnExitCannonMode;
            initialized = true;
        }

        if (!isInCannonMode)
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
        if (IsActive && direction.magnitude != 0 && !isInCannonMode)
        {
            playerManager.MovesController.onObjectSpawned += (obj) =>
            {
                if (obj.TryGetComponent(out DuraFlashCannonObj projectile))
                {
                    flashCannonInstance = projectile;
                    projectile.InitializeRPC(direction, playerManager.transform.position, playerManager.CurrentTeam.Team, damageInfo, IsUpgraded);
                }
            };

            playerManager.MovesController.SpawnNetworkObjectFromStringRPC(assetPath);

            playerManager.transform.rotation = Quaternion.LookRotation(direction);
            duraBasicAtk.EnterCannonMode();
            isInCannonMode = true;

            BattleUIManager.instance.ShowMoveSecondaryCooldown(0, 6f);

            wasMoveSuccessful = false;
        } else if (IsActive && isInCannonMode)
        {
            duraBasicAtk.ExitCannonMode();
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideSkillshotAim();
        base.Finish();
    }

    private void OnExitCannonMode()
    {
        isInCannonMode = false;
        BattleUIManager.instance.ShowMoveSecondaryCooldown(0, 0);
        if (flashCannonInstance != null)
        {
            flashCannonInstance.DestroySelfRPC();
            flashCannonInstance = null;
        }

        wasMoveSuccessful = true;
        base.Finish();
    }

    public override void Cancel()
    {
        Aim.Instance.HideSkillshotAim();
        base.Cancel();
    }

    public override void ResetMove()
    {
        duraBasicAtk.ExitCannonMode();
    }
}
