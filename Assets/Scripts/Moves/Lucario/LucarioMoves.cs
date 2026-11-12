using UnityEngine;

public abstract class LucarioMove : PrefabMove
{
    private int request;
    public bool Recasting { get; private set; }
    public override int ExecutionRequest => request;
    public override string PrefabPath => LucarioMoveRules.Folder + MoveId + ".prefab";
    public override bool RequiresServerAcceptance => true;
    public override bool LockDuringLoad => true;
    public override bool CooldownOnEffectEnd => true;
    public override string IndicatorPath => LucarioMoveRules.Visuals + "LineIndicator";
    protected virtual float Width => 2;
    public override Vector3 IndicatorScale => new Vector3(Width, 1, Range);
    public override void Start(PlayerManager controller)
    {
        if (IsActive) return;
        if (!Recasting) request = controller.MovesController.NextLucarioRequest();
        base.Start(controller);
    }
    protected override void OnExecutionState(bool finished) { Recasting = !finished; }
    public void CompleteExecution(float remaining)
    {
        ReceiveExecutionState(true);
        if (playerManager != null) playerManager.MovesController.SetLucarioOwnerCooldown(this, remaining);
    }
    public override void ResetMove() { base.ResetMove(); Recasting = false; request = 0; }
}
public class LucarioQuickAttack : LucarioMove
{
    public override AvailableMoves MoveId => AvailableMoves.LucarioQuickAttack;
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => LucarioMoveRules.QuickRange;
    protected override float Width => LucarioMoveRules.QuickWidth;
    public LucarioQuickAttack() { Name = "Quick Attack"; Cooldown = 6; }
}
public class LucarioExtremeSpeed : LucarioMove
{
    public override AvailableMoves MoveId => AvailableMoves.LucarioExtremeSpeed;
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => LucarioMoveRules.ExtremeRange;
    protected override float Width => LucarioMoveRules.ExtremeWidth;
    public LucarioExtremeSpeed() { Name = "Extreme Speed"; Cooldown = 7.5f; }
}
public class LucarioPowerUpPunch : LucarioMove
{
    private bool aiming;
    private float began, report;
    public bool IsChargingAim => aiming;
    public override AvailableMoves MoveId => AvailableMoves.LucarioPowerUpPunch;
    public override MoveAimStyle AimStyle => MoveAimStyle.Dash;
    public override float Range => LucarioMoveRules.PunchRange(aiming ? Time.time - began : 0);
    protected override float Width => LucarioMoveRules.PunchWidth;
    public LucarioPowerUpPunch() { Name = "Power-Up Punch"; Cooldown = 7; }
    public override void Start(PlayerManager controller)
    {
        if (aiming || IsActive) return;
        began = Time.time; aiming = true;
        base.Start(controller); base.Finish(); IsActive = aiming;
        if (aiming) { ActorAim.InitializeDashAim(Range); ActorAim.ShowCustomIndicator(IndicatorPath, IndicatorScale); report = 0; }
    }
    public override void Update()
    {
        if (!aiming) return;
        if (playerManager.Pokemon.CurrentHp <= 0) { Cancel(); return; }
        direction = ActorAim.DashAim(); ActorAim.InitializeDashAim(Range);
        ActorAim.ShowCustomIndicator(IndicatorPath, IndicatorScale); ActorAim.UpdateCustomIndicator(direction);
        report -= Time.deltaTime;
        if (report <= 0) { playerManager.MovesController.LucarioPunchInputRPC(direction, false, false, ExecutionRequest); report = 0.05f; }
    }
    private void StopAim(bool cancel, bool send)
    {
        if (!aiming) return;
        aiming = false; IsActive = false; ActorAim.HideDashAim(); ActorAim.HideCustomIndicator();
        if (send && playerManager.MovesController.IsSpawned) playerManager.MovesController.LucarioPunchInputRPC(direction, true, cancel, ExecutionRequest);
    }
    public override void Finish() { StopAim(false, true); }
    public override void Cancel() { StopAim(true, true); base.Cancel(); }
    internal override void AcceptExecution() { base.AcceptExecution(); IsActive = aiming; }
    protected override void OnExecutionRejected() { StopAim(true, false); }
    protected override void OnExecutionState(bool finished) { base.OnExecutionState(finished); if (finished) StopAim(true, false); }
    public override void ResetMove() { StopAim(true, true); base.ResetMove(); }
}
public class LucarioMeteorMash : LucarioMove
{
    public override AvailableMoves MoveId => AvailableMoves.LucarioMeteorMash;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => LucarioMoveRules.MeteorRange;
    protected override float Width => LucarioMoveRules.MeteorWidth;
    public LucarioMeteorMash() { Name = "Meteor Mash"; Cooldown = 7; }
}
public class LucarioBoneRush : LucarioMove
{
    public override AvailableMoves MoveId => AvailableMoves.LucarioBoneRush;
    public override MoveAimStyle AimStyle => Recasting ? MoveAimStyle.None : MoveAimStyle.Direction;
    public override string IndicatorPath => Recasting ? null : base.IndicatorPath;
    public override float Range => LucarioMoveRules.BoneRange;
    protected override float Width => LucarioMoveRules.BoneRadius * 2;
    public LucarioBoneRush() { Name = "Bone Rush"; Cooldown = 9; }
}
public class LucarioCloseCombat : LucarioMove
{
    public override AvailableMoves MoveId => AvailableMoves.LucarioCloseCombat;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => LucarioMoveRules.CombatRange;
    protected override float Width => LucarioMoveRules.CombatWidth;
    public LucarioCloseCombat() { Name = "Close Combat"; Cooldown = 6.5f; }
}
public class LucarioAuraCannon : LucarioMove
{
    public override AvailableMoves MoveId => AvailableMoves.LucarioAuraCannon;
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => LucarioMoveRules.AuraRange;
    protected override float Width => LucarioMoveRules.AuraRadius * 2;
    public override float CameraReturnDelay => 0.5f;
    public LucarioAuraCannon() { Name = "Aura Cannon"; Cooldown = 0; }
}
