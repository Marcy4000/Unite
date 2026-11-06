using UnityEngine;

// Custom held cone attack. Release ends the hitbox; remaining crystals continue
// to drain before its original five-second cooldown begins.
public class GlaceIcicleSpear : GlaceonMove
{
    public override AvailableMoves MoveId => AvailableMoves.GlaceIcycleSpear;
    public override string PrefabPath => GlaceonMoveRules.Folder + "IcicleSpear.prefab";
    public override MoveAimStyle AimStyle => MoveAimStyle.Direction;
    public override float Range => 9;
    public override bool CooldownOnEffectEnd => true;
    protected override bool ReusesExecutionRequest(PlayerManager controller) => draining;
    private bool aiming, draining;
    private float report;
    public bool IsChannelAiming => aiming;
    public GlaceIcicleSpear() { Name = "Icicle Spear"; Cooldown = 5; }
    public override void Start(PlayerManager controller)
    {
        if (aiming || IsActive) return;
        base.Start(controller);
        if (!IsActive) return;
        aiming = true;
        base.Finish();
        IsActive = aiming;
        if (!aiming) return; // Host rejection can arrive synchronously.
        ActorAim.InitializeSkillshotAim(Range);
        report = 0;
    }
    public override void Update()
    {
        if (!aiming) return;
        if (playerManager.Pokemon.CurrentHp <= 0 || playerManager.Pokemon.HasAnyStatusEffect(GlaceonMoveRules.Interrupts)) { ResetMove(); return; }
        direction = ActorAim.SkillshotAim();
        if (direction.sqrMagnitude > 0.001f) playerManager.transform.rotation = Quaternion.LookRotation(direction);
        report -= Time.deltaTime;
        if (report <= 0)
        {
            playerManager.MovesController.GlaceSpearInputRPC(direction, false, ExecutionRequest);
            report = 0.05f;
        }
    }
    public override void Finish()
    {
        if (!aiming) return;
        aiming = false; IsActive = false; ActorAim.HideSkillshotAim();
        if (playerManager.MovesController.IsSpawned) playerManager.MovesController.GlaceSpearInputRPC(direction, true, ExecutionRequest);
    }
    public override void Cancel() { Finish(); base.Cancel(); }
    // Bots use IsActive to distinguish a held action from a finished cast.
    internal override void AcceptExecution() { draining = true; base.AcceptExecution(); IsActive = aiming; }
    protected override void OnExecutionRejected() { Finish(); }
    protected override void OnExecutionState(bool finished) { draining = !finished; if (finished) Finish(); }
    public override void ResetMove() { Finish(); base.ResetMove(); draining = false; }
}
