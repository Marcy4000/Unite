using UnityEngine;

public abstract class CombatMove : PrefabMove
{
    private float nextInputUpdate;
    public override string PrefabPath => null;
    public override bool CooldownOnFinish => false;
    public virtual bool StartsOnPress => false;
    public virtual bool EndsOnRelease => true;

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        if (StartsOnPress && IsActive) controller.MovesController.Combat?.Submit(this, direction, target);
    }

    public override void Update()
    {
        base.Update();
        if (!StartsOnPress || !IsActive || Time.time < nextInputUpdate) return;
        nextInputUpdate = Time.time + 0.05f;
        playerManager.MovesController.Combat?.UpdateInput(this, direction);
    }

    public override void Finish()
    {
        if (!IsActive || playerManager == null) return;
        if (StartsOnPress)
        {
            if (EndsOnRelease) Cancel();
            else
            {
                EndInput();
                playerManager.MovesController.Combat?.SetInputActive(this, false);
            }
            return;
        }
        UpdateAim();
        bool valid = AimStyle == MoveAimStyle.Target
            ? target != null && IsTargetInCastRange(playerManager.transform.position, target.transform.position)
            : AimStyle == MoveAimStyle.None || AimStyle == MoveAimStyle.Circle || AimStyle == MoveAimStyle.Ground || direction.sqrMagnitude > 0.001f;
        HideAim(!valid);
        IsActive = false;
        if (valid) playerManager.MovesController.Combat?.Submit(this, direction, target);
    }

    internal void Accept()
    {
        bool held = StartsOnPress && IsActive;
        wasMoveSuccessful = true;
        CompleteAcceptedInput();
        if (held) IsActive = true;
    }

    internal void EndInput() { if (IsActive) HideAim(true); IsActive = false; }

    public override void Cancel()
    {
        bool held = StartsOnPress && IsActive;
        base.Cancel();
        if (held) playerManager?.MovesController.Combat?.Cancel(this);
    }

    public override void ResetMove()
    {
        Cancel();
        target = null;
        if (playerManager != null && playerManager.IsSpawned && playerManager.IsOwner)
            playerManager.MovesController.Combat?.Cancel(this);
    }
}
