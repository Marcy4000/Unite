using UnityEngine;

public class JoltElectroweb : MoveBase
{
    private Vector3 direction;
    public JoltElectroweb() { Name = "Electroweb"; Cooldown = 10f; }
    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        ActorAim.InitializeSkillshotAim(JoltMoveRules.WebRange);
        direction = ActorAim.SkillshotAim();
    }
    public override void Update() { if (IsActive) direction = ActorAim.SkillshotAim(); }
    public override void Finish()
    {
        if (IsActive && direction.sqrMagnitude > 0.001f)
        {
            bool empowered = (playerManager.PassiveController.Passive as JoltPassive)?.TryConsumeCharge() == true;
            playerManager.transform.rotation = Quaternion.LookRotation(direction);
            JoltMoveRuntime.Effect(playerManager, JoltEffect.Electroweb, empowered, IsUpgraded);
            playerManager.StopMovementForTime(0.15f, false);
            wasMoveSuccessful = true;
        }
        ActorAim.HideSkillshotAim();
        base.Finish();
    }
    public override void Cancel() { ActorAim.HideSkillshotAim(); base.Cancel(); }
    public override void ResetMove() { Cancel(); }
}
