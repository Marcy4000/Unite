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
        ActorAim.InitializeCircleAreaIndicator(7f);
        position = ActorAim.CircleAreaAim();
    }

    public override void Update()
    {
        if (IsActive) position = ActorAim.CircleAreaAim();
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
        ActorAim.HideCircleAreaIndicator();
        base.Finish();
    }

    public override void Cancel()
    {
        ActorAim.HideCircleAreaIndicator();
        base.Cancel();
    }

    public override void ResetMove() { Cancel(); }
}
