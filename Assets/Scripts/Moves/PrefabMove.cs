using UnityEngine;

public enum MoveAimStyle { None, Target, Direction, Dash, Circle }

/// <summary>Normal MoveBase input/cooldown flow for a server-owned, authored effect.</summary>
public abstract class PrefabMove : MoveBase
{
    public abstract AvailableMoves MoveId { get; }
    public abstract string PrefabPath { get; }
    public virtual MoveAimStyle AimStyle => MoveAimStyle.None;
    public virtual float Range => 0;
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
    }

    private void UpdateAim()
    {
        if (AimStyle == MoveAimStyle.Target) target = Aim.Instance.SureHitAim()?.GetComponent<Pokemon>();
        else if (AimStyle == MoveAimStyle.Direction) direction = Aim.Instance.SkillshotAim();
        else if (AimStyle == MoveAimStyle.Dash) direction = Aim.Instance.DashAim();
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
        switch (AimStyle)
        {
            case MoveAimStyle.Target: Aim.Instance.HideAutoAim(); break;
            case MoveAimStyle.Direction: Aim.Instance.HideSkillshotAim(); break;
            case MoveAimStyle.Dash: Aim.Instance.HideDashAim(); break;
            case MoveAimStyle.Circle: Aim.Instance.HideSimpleCircle(); break;
        }
    }

    public override void Cancel() { if (IsActive) HideAim(); base.Cancel(); }
    public override void ResetMove() { Cancel(); target = null; }
}
