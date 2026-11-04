public abstract class ZoroarkMove : PrefabMove
{
    private bool aerialCast;
    public override string PrefabPath => ZoroarkMoveRules.Folder + MoveId + ".prefab";
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => aerialCast ? ZoroarkMoveRules.NightDashRange : ZoroarkMoveRules.Range(MoveId);
    public override bool LockDuringLoad => true;
    public override bool CooldownOnFinish => !aerialCast && base.CooldownOnFinish;
    public override void Start(PlayerManager controller)
    {
        aerialCast = controller.MovesController.ZoroarkAerialOpen && (MoveId == AvailableMoves.ZoroarkSlash
            || MoveId == AvailableMoves.ZoroarkShadowClaw || MoveId == AvailableMoves.ZoroarkCut);
        base.Start(controller);
    }
    protected override void OnCommitted()
    {
        if (aerialCast) playerManager.MovesController.SpendZoroarkAerialReposition();
    }
}
public class ZoroarkSlash : ZoroarkMove
{
    public ZoroarkSlash() { Name = "Slash"; Cooldown = 5.5f; }
    public override AvailableMoves MoveId => AvailableMoves.ZoroarkSlash;
}
public class ZoroarkFurySwipes : ZoroarkMove
{
    public ZoroarkFurySwipes() { Name = "Fury Swipes"; Cooldown = 7.5f; }
    public override AvailableMoves MoveId => AvailableMoves.ZoroarkFurySwipes;
    public override bool CooldownOnEffectEnd => true;
}
public class ZoroarkShadowClaw : ZoroarkMove
{
    public ZoroarkShadowClaw() { Name = "Shadow Claw"; Cooldown = 4.5f; }
    public override AvailableMoves MoveId => AvailableMoves.ZoroarkShadowClaw;
}
public class ZoroarkCut : ZoroarkMove
{
    public ZoroarkCut() { Name = "Cut"; Cooldown = 4.5f; }
    public override AvailableMoves MoveId => AvailableMoves.ZoroarkCut;
}
public class ZoroarkNightSlash : ZoroarkMove
{
    public ZoroarkNightSlash() { Name = "Night Slash"; Cooldown = 7; }
    public override AvailableMoves MoveId => AvailableMoves.ZoroarkNightSlash;
    public override MoveAimStyle AimStyle => MoveAimStyle.Circle;
    public override float Range => ZoroarkMoveRules.UniqueRange;
    public override bool CooldownOnEffectEnd => true;
    public override float EffectiveCooldown(bool upgraded) => upgraded ? 6 : 7;
}
public class ZoroarkFeintAttack : ZoroarkMove
{
    public ZoroarkFeintAttack() { Name = "Feint Attack"; Cooldown = 6; }
    public override AvailableMoves MoveId => AvailableMoves.ZoroarkFeintAttack;
    public override float Range => ZoroarkMoveRules.FeintAimRange(playerManager != null && playerManager.MovesController.ZoroarkFeintActive,
        playerManager != null && playerManager.MovesController.ZoroarkFeintReady);
    public override MoveAimStyle AimStyle => playerManager != null && playerManager.MovesController.ZoroarkFeintReady ? MoveAimStyle.Target : MoveAimStyle.Dash;
    public override bool CooldownOnEffectEnd => true;
    public override float EffectiveCooldown(bool upgraded) => upgraded ? 5 : 6;
}
public class ZoroarkNightfallDaze : ZoroarkMove
{
    public ZoroarkNightfallDaze() { Name = "Nightfall Daze"; Cooldown = 0; }
    public override AvailableMoves MoveId => AvailableMoves.ZoroarkNightfallDaze;
    public override MoveAimStyle AimStyle => MoveAimStyle.Circle;
    public override float Range => ZoroarkMoveRules.UniteRadii[2];
}
