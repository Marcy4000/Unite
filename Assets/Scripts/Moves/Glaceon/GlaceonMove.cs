using UnityEngine;

public abstract class GlaceonMove : PrefabMove
{
    public override string PrefabPath => GlaceonMoveRules.Folder + "GlaceMoveEffect.prefab";
    public override bool LockDuringLoad => true;
    private int executionRequest;
    public override int ExecutionRequest => executionRequest;
    public override bool RequiresServerAcceptance => true;
    public override bool CooldownOnFinish => false;
    public override float TargetRangeTolerance => 0.5f;
    protected virtual bool ReusesExecutionRequest(PlayerManager controller) => false;
    public override void Start(PlayerManager controller)
    {
        if (IsActive || controller.Pokemon.CurrentHp <= 0 || controller.Pokemon.HasAnyStatusEffect(GlaceonMoveRules.Interrupts)) return;
        if (!ReusesExecutionRequest(controller)) executionRequest = controller.MovesController.NextGlaceRequest();
        Cooldown = GlaceonMoveRules.Cooldown(MoveId);
        base.Start(controller);
    }
    public override void ResetMove()
    {
        base.ResetMove();
        if (playerManager != null) executionRequest = playerManager.MovesController.NextGlaceRequest();
    }
    public override void Update()
    {
        if (IsActive && (playerManager.Pokemon.CurrentHp <= 0 || playerManager.Pokemon.HasAnyStatusEffect(GlaceonMoveRules.Interrupts))) { Cancel(); return; }
        base.Update();
    }
}
