public class VaporDive : CombatMove
{
    public override AvailableMoves MoveId => AvailableMoves.VaporDive;
    public override MoveAimStyle AimStyle => MoveAimStyle.None;
    public override bool CooldownOnEffectEnd => true;
    public override float EffectiveCooldown(bool upgraded) => VaporMoveRules.DiveCooldown(upgraded);

    public VaporDive() { Name = "Dive"; Cooldown = VaporMoveRules.DiveCooldown(false); }

    public override void Finish()
    {
        if (!IsActive || playerManager == null) return;
        IsActive = false;
        playerManager.MovesController.Combat?.Submit(this, playerManager.transform.position, null);
    }
}
