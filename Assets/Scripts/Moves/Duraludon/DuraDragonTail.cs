using UnityEngine;

public class DuraDragonTail : MoveBase
{
    public override MoveUseType UseType => MoveUseType.StoredUses;
    public override int MaxStoredUses => 2;
    public override float StoredUseInterval => 2.5f;
    private DuraBoostedObj Combat => (playerManager.MovesController.BasicAttack as DuraBasicAtk)?.Combat;
    private Vector3 direction;

    public DuraDragonTail()
    {
        Name = "Dragon Tail";
        Cooldown = 14f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        if (!IsActive) return;
        Aim.Instance.InitializeSkillshotAim(4f);
        direction = Aim.Instance.SkillshotAim();
    }

    public override void Update()
    {
        if (IsActive) direction = Aim.Instance.SkillshotAim();
    }

    public override void Finish()
    {
        if (IsActive && direction.sqrMagnitude > 0 && Combat != null)
        {
            var attack = playerManager.MovesController.BasicAttack as DuraBasicAtk;
            Combat.DragonTailRPC(direction, IsUpgraded);
            playerManager.transform.rotation = Quaternion.LookRotation(direction);
            playerManager.AnimationManager.PlayAnimation(attack.IsInCannonMode ? "ani_spell1a2a_bat_0884" : "ani_spell2a_bat_0884");
            // StartDash is blocked by Cannon Mode's movement restriction. Briefly
            // release just that restriction; the stance itself remains active.
            attack.DashBackwards(direction);
            attack.GainBoostedAttack();
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

    public override void ResetMove() { Cancel(); }
}
