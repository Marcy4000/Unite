using UnityEngine;

public enum MoveAimStyle { None, Target, Direction, Dash, Circle, Ground }

/// <summary>Normal MoveBase input/cooldown flow for a server-owned, authored effect.</summary>
public abstract class PrefabMove : MoveBase
{
    public abstract AvailableMoves MoveId { get; }
    public abstract string PrefabPath { get; }
    public virtual MoveAimStyle AimStyle => MoveAimStyle.None;
    public virtual float Range => 0;
    public virtual float IndicatorRange => Range;
    public virtual float TargetRangeTolerance => 0.5f;
    public virtual float CameraReturnDelay => 0; // Opt-in directional aim camera.
    public virtual float AreaRadius => 1;
    public virtual float EffectiveCooldown(bool upgraded) => Cooldown;
    public virtual bool CooldownOnEffectEnd => false;
    public override bool CooldownOnFinish => !CooldownOnEffectEnd;
    public virtual bool IsOffensive => true;
    public virtual bool LockDuringLoad => false;
    public virtual bool UsesExplicitUniteCastCheckpoint => false;
    public virtual string IndicatorPath => null;
    public virtual Vector3 IndicatorScale => Vector3.one;
    public virtual bool MirrorsIndicator => false;
    private System.IDisposable pending;
    private CombatActionLock loadLock;
    private CameraController aimCamera;
    protected Vector3 direction;
    protected Pokemon target;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        if (controller.IsLocalHuman && CameraReturnDelay <= 0) CameraController.Instance?.ClearSkillAim();
        target = null;
        direction = controller.transform.forward;
        switch (AimStyle)
        {
            case MoveAimStyle.Target: playerManager.Aim.InitializeAutoAim(IndicatorRange, 90, AimTarget.NonAlly); break;
            case MoveAimStyle.Direction: playerManager.Aim.InitializeSkillshotAim(IndicatorRange); break;
            case MoveAimStyle.Dash: playerManager.Aim.InitializeDashAim(IndicatorRange); break;
            case MoveAimStyle.Circle: playerManager.Aim.InitializeSimpleCircle(Range); break;
            case MoveAimStyle.Ground: playerManager.Aim.InitializeSizedCircleAreaIndicator(Range, AreaRadius); break;
        }
        UpdateAim();
        if (IndicatorPath != null) playerManager.Aim.ShowCustomIndicator(IndicatorPath, IndicatorScale, MirrorsIndicator);
    }

    protected void UpdateAim()
    {
        if (AimStyle == MoveAimStyle.Target) target = playerManager.Aim.SureHitAim()?.GetComponent<Pokemon>();
        else if (AimStyle == MoveAimStyle.Direction) direction = playerManager.Aim.SkillshotAim();
        else if (AimStyle == MoveAimStyle.Dash) direction = playerManager.Aim.DashAim();
        else if (AimStyle == MoveAimStyle.Ground) direction = playerManager.Aim.CircleAreaAim() - playerManager.transform.position;
        if (IndicatorPath != null) playerManager.Aim.UpdateCustomIndicator(direction);
        if (CameraReturnDelay > 0 && playerManager.IsLocalHuman)
        {
            aimCamera = CameraController.Instance;
            if (aimCamera != null) aimCamera.UpdateSkillAim(this, direction, IndicatorRange, CameraReturnDelay);
        }
    }

    public override void Update() { if (IsActive) UpdateAim(); }

    public override void Finish()
    {
        if (!IsActive) return;
        UpdateAim();
        bool valid = AimStyle == MoveAimStyle.Target ? target != null && IsTargetInCastRange(playerManager.transform.position, target.transform.position)
            : AimStyle == MoveAimStyle.Ground || direction.sqrMagnitude > 0.001f;
        HideAim(!valid);
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

    public bool IsTargetInCastRange(Vector3 origin, Vector3 position) => CombatTargeting.Finite(origin) && CombatTargeting.Finite(position)
        && Vector3.Distance(origin, position) <= Range + TargetRangeTolerance;

    protected void HideAim(bool immediateCameraReturn = false)
    {
        if (aimCamera != null) aimCamera.EndSkillAim(this, immediateCameraReturn);
        if (playerManager == null || playerManager.Aim == null) return;
        if (IndicatorPath != null) playerManager.Aim.HideCustomIndicator();
        switch (AimStyle)
        {
            case MoveAimStyle.Target: playerManager.Aim.HideAutoAim(); break;
            case MoveAimStyle.Direction: playerManager.Aim.HideSkillshotAim(); break;
            case MoveAimStyle.Dash: playerManager.Aim.HideDashAim(); break;
            case MoveAimStyle.Circle: playerManager.Aim.HideSimpleCircle(); break;
            case MoveAimStyle.Ground: playerManager.Aim.HideCircleAreaIndicator(); break;
        }
    }

    public override void Cancel()
    {
        if (IsActive) HideAim(true); else if (aimCamera != null) aimCamera.EndSkillAim(this, true);
        base.Cancel();
    }
    internal void ReceiveExecutionState(bool finished)
    {
        pending?.Dispose(); pending = null;
        if (finished && IsActive) Cancel(); // A recast window can expire while aiming.
        OnExecutionState(finished);
        if (finished && playerManager != null) playerManager.MovesController.BeginMoveCooldown(this);
    }
    protected virtual void OnExecutionState(bool finished) { }
    protected void CompleteAcceptedInput() { base.Finish(); }
    internal void ReleaseLoadLock() { loadLock?.Dispose(); loadLock = null; }
    internal void RejectExecution() { pending?.Dispose(); pending = null; ReleaseLoadLock(); }

    public override void ResetMove()
    {
        Cancel(); target = null; pending?.Dispose(); pending = null; ReleaseLoadLock();
        if (playerManager != null && playerManager.IsSpawned && playerManager.IsOwner)
            playerManager.MovesController.CancelPrefabMoveRPC(MoveId);
    }
}
