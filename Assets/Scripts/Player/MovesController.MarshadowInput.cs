using UnityEngine;

public partial class MovesController
{
    private int marshadowBufferedSlot = -1;
    private float marshadowBufferUntil;
    private bool marshadowBufferReleased;
    private Vector3 marshadowBufferDirection;

    private void BufferMarshadowInput(int slot, bool released)
    {
        if (!IsLivingMarshadow || playerManager.PlayerState != PlayerState.Alive || playerManager.Aim == null) return;
        if (released)
        {
            if (marshadowBufferedSlot == slot) marshadowBufferReleased = true;
            return;
        }
        if (!moveStatuses[slot].HasStatus(ActionStatusType.Disabled) || moveStatuses[slot].HasStatus(ActionStatusType.Stunned)
            || moveStatuses[slot].HasStatus(ActionStatusType.Cooldown)) return;
        bool inCast = (moves[0] is MarshadowMoveBase a && a.IsExecuting) || (moves[1] is MarshadowMoveBase b && b.IsExecuting);
        if (!inCast) return;
        marshadowBufferedSlot = slot; marshadowBufferReleased = false;
        marshadowBufferUntil = Time.time + 0.18f;
        marshadowBufferDirection = playerManager.Aim.SkillshotAim();
    }
    private void ProcessMarshadowBufferedInput()
    {
        if (marshadowBufferedSlot < 0) return;
        if (playerManager.IsLocalHuman && controls.Movement.CancelMove.WasPressedThisFrame()) { ClearMarshadowBufferedInput(); return; }
        if (!IsLivingMarshadow || Time.time > marshadowBufferUntil) { ClearMarshadowBufferedInput(); return; }
        int slot = marshadowBufferedSlot;
        if (!moveStatuses[slot].HasStatus(ActionStatusType.None)) return;
        bool released = marshadowBufferReleased;
        Vector3 direction = marshadowBufferDirection;
        ClearMarshadowBufferedInput();
        moves[slot].Start(playerManager);
        if (moves[slot] is MarshadowMoveBase move) move.SetBufferedDirection(direction);
        if (released) moves[slot].Finish();
    }
    private void ClearMarshadowBufferedInput() { marshadowBufferedSlot = -1; marshadowBufferReleased = false; }
}
