using UnityEngine;

public class CinderLowSweep : MoveBase
{
    private Vector3 direction;
    private DamageInfo damageInfo;

    public CinderLowSweep()
    {
        Name = "Low Sweep";
        Cooldown = 7.5f;
        damageInfo = new DamageInfo(0, 0.36f, 3, 100, DamageType.Physical);
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeDashAim(5f);
        damageInfo.attackerId = controller.Pokemon.NetworkObjectId;
        Debug.Log("Executed low Sweep!");
    }

    public override void Update()
    {
        if (!IsActive)
        {
            return;
        }
        direction = Aim.Instance.DashAim();
    }

    public override void Finish()
    {
        if (IsActive)
        {
            playerManager.PlayerMovement.StartDash(direction);
            playerManager.AnimationManager.PlayAnimation($"ani_spell2_bat_0815");
            playerManager.transform.rotation = Quaternion.LookRotation(direction);
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideDashAim();
        base.Finish();
    }

    public override void Cancel()
    {
        base.Cancel();
        Aim.Instance.HideDashAim();
    }
}
