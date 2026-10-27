using UnityEngine;

public enum MoveAimStyle { None, Target, Direction, Dash, Circle }

/// <summary>Normal MoveBase input/cooldown flow for a server-owned, authored effect.</summary>
public abstract class PrefabMove : MoveBase
{
    public abstract AvailableMoves MoveId { get; }
    public abstract string PrefabPath { get; }
    public virtual MoveAimStyle AimStyle => MoveAimStyle.None;
    public virtual float Range => 0;
    public virtual bool CooldownOnEffectEnd => false;
    public override bool CooldownOnFinish => !CooldownOnEffectEnd;
    public virtual bool IsOffensive => true;
    public virtual bool LockDuringLoad => false;
    public virtual string IndicatorPath => null;
    public virtual Vector3 IndicatorScale => Vector3.one;
    public virtual bool MirrorsIndicator => false;
    private System.IDisposable pending;
    private CombatActionLock loadLock;
    protected Vector3 direction;
    protected Pokemon target;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        target = null;
        direction = controller.transform.forward;
        switch (AimStyle)
        {
            case MoveAimStyle.Target: Aim.Instance.InitializeAutoAim(Range, 90, AimTarget.NonAlly); break;
            case MoveAimStyle.Direction: Aim.Instance.InitializeSkillshotAim(Range); break;
            case MoveAimStyle.Dash: Aim.Instance.InitializeDashAim(Range); break;
            case MoveAimStyle.Circle: Aim.Instance.InitializeSimpleCircle(Range); break;
        }
        UpdateAim();
        if (IndicatorPath != null) Aim.Instance.ShowCustomIndicator(IndicatorPath, IndicatorScale, MirrorsIndicator);
    }

    private void UpdateAim()
    {
        if (AimStyle == MoveAimStyle.Target) target = Aim.Instance.SureHitAim()?.GetComponent<Pokemon>();
        else if (AimStyle == MoveAimStyle.Direction) direction = Aim.Instance.SkillshotAim();
        else if (AimStyle == MoveAimStyle.Dash) direction = Aim.Instance.DashAim();
        if (IndicatorPath != null) Aim.Instance.UpdateCustomIndicator(direction);
    }

    public override void Update() { if (IsActive) UpdateAim(); }

    public override void Finish()
    {
        if (!IsActive) return;
        UpdateAim();
        bool valid = AimStyle == MoveAimStyle.Target ? target != null : direction.sqrMagnitude > 0.001f;
        HideAim();
        if (valid)
        {
            if (LockDuringLoad) { loadLock?.Dispose(); loadLock = new CombatActionLock(playerManager, true, false); }
            if (CooldownOnEffectEnd)
            {
                pending?.Dispose();
                pending = playerManager.MovesController.AcquireMoveStatus((int)playerManager.MovesController.GetMoveSlot(this), ActionStatusType.Busy);
            }
            playerManager.MovesController.SpawnMoveEffectRPC(MoveId, direction, target != null ? target.NetworkObjectId : 0);
            OnCommitted();
            wasMoveSuccessful = true;
        }
        base.Finish();
    }

    protected virtual void OnCommitted() { }

    private void HideAim()
    {
        if (Aim.Instance == null) return;
        if (IndicatorPath != null) Aim.Instance.HideCustomIndicator();
        switch (AimStyle)
        {
            case MoveAimStyle.Target: Aim.Instance.HideAutoAim(); break;
            case MoveAimStyle.Direction: Aim.Instance.HideSkillshotAim(); break;
            case MoveAimStyle.Dash: Aim.Instance.HideDashAim(); break;
            case MoveAimStyle.Circle: Aim.Instance.HideSimpleCircle(); break;
        }
    }

    public override void Cancel() { if (IsActive) HideAim(); base.Cancel(); }
    internal void ReceiveExecutionState(bool finished)
    {
        pending?.Dispose(); pending = null;
        if (finished && IsActive) Cancel(); // A recast window can expire while aiming.
        OnExecutionState(finished);
        if (finished && playerManager != null) playerManager.MovesController.BeginMoveCooldown(this);
    }
    protected virtual void OnExecutionState(bool finished) { }
    internal void ReleaseLoadLock() { loadLock?.Dispose(); loadLock = null; }
    internal void RejectExecution() { pending?.Dispose(); pending = null; ReleaseLoadLock(); }

    public override void ResetMove()
    {
        Cancel(); target = null; pending?.Dispose(); pending = null; ReleaseLoadLock();
        if (playerManager != null && playerManager.IsSpawned && playerManager.IsOwner)
            playerManager.MovesController.CancelPrefabMoveRPC(MoveId);
    }
}
