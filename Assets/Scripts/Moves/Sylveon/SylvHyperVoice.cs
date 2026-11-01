using UnityEngine;

public class SylvHyperVoice : CombatMove
{
    private const int wavesAmountNormal = SylvMoveRules.HyperVoiceWavesNormal;
    private const int wavesAmountUpgraded = SylvMoveRules.HyperVoiceWavesUpgraded;

    private DamageInfo closeDamage;
    private DamageInfo farDamage;

    public override AvailableMoves MoveId => AvailableMoves.SylvHyperVoice;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => SylvMoveRules.HyperVoiceRange;
    public override bool LockDuringLoad => true;
    public override bool LockMovementDuringLoad => false;

    public SylvHyperVoice()
    {
        Name = "Hyper Voice";
        Cooldown = SylvMoveRules.HyperVoiceCooldown;
        closeDamage = new DamageInfo(0, 0.32f, 6, 110, DamageType.Special);
        farDamage = new DamageInfo(0, 0.54f, 8, 184, DamageType.Special);
    }

    public override void Start(PlayerManager controller)
    {
        controller?.MovesController?.EnsureSylveonCombatRuntime();
        base.Start(controller);
        if (controller?.Pokemon != null)
        {
            closeDamage.attackerId = controller.Pokemon.NetworkObjectId;
            farDamage.attackerId = controller.Pokemon.NetworkObjectId;
        }
        if (playerManager?.Aim != null)
        {
            playerManager.Aim.HideSkillshotAim();
            playerManager.Aim.InitializeHyperVoiceAim();
        }
    }

    public override void Finish()
    {
        if (playerManager?.Aim != null)
            playerManager.Aim.HideHyperVoiceAim();
        base.Finish();
    }

    public override void Cancel()
    {
        if (playerManager?.Aim != null)
            playerManager.Aim.HideHyperVoiceAim();
        base.Cancel();
    }
}
