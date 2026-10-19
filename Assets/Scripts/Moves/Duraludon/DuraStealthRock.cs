using UnityEngine;

public class DuraStealthRock : MoveBase
{
    public override MoveUseType UseType => MoveUseType.StoredUses;
    public override int MaxStoredUses => IsUpgraded ? 3 : 2;
    public override float StoredUseInterval => 1f;
    private DuraBoostedObj Combat => (playerManager.MovesController.BasicAttack as DuraBasicAtk)?.Combat;
    private Vector3 position;

    public DuraStealthRock()
    {
        Name = "Stealth Rock";
        Cooldown = 8.5f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        if (!IsActive) return;
        Aim.Instance.InitializeCircleAreaIndicator(7f);
        position = Aim.Instance.CircleAreaAim();
    }

    public override void Update()
    {
        if (IsActive) position = Aim.Instance.CircleAreaAim();
    }

    public override void Finish()
    {
        if (IsActive && Combat != null)
        {
            Combat.StealthRockRPC(position);
            var attack = playerManager.MovesController.BasicAttack as DuraBasicAtk;
            Vector3 direction = position - playerManager.transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0) playerManager.transform.rotation = Quaternion.LookRotation(direction);
            playerManager.AnimationManager.PlayAnimation(attack.IsInCannonMode ? "ani_spell1a2b_bat_0884" : "ani_spell2b_bat_0884");
            attack.GainBoostedAttack();
            wasMoveSuccessful = true;
        }
        Aim.Instance.HideCircleAreaIndicator();
        base.Finish();
    }

    public override void Cancel()
    {
        Aim.Instance.HideCircleAreaIndicator();
        base.Cancel();
    }

    public override void ResetMove() { Cancel(); }
}
