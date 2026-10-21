using UnityEngine;

public partial class MovesController
{
    private int vaporBufferedSlot = -1;
    private float vaporBufferUntil;
    private bool vaporBufferReleased;
    private void BufferVaporeonInput(int slot, bool released)
    {
        if (!IsLivingVaporeon || playerManager.PlayerState != PlayerState.Alive) return;
        if (released) { if (vaporBufferedSlot == slot) vaporBufferReleased = true; return; }
        if (moveStatuses[slot].HasStatus(ActionStatusType.Stunned) || moveStatuses[slot].HasStatus(ActionStatusType.Cooldown)) return;
        bool nearRecovery = false;
        foreach (MoveBase move in moves)
            if (move is VaporMoveBase cast && cast.IsExecuting && cast.RecoveryRemaining <= 0.18f) nearRecovery = true;
        if (uniteMove is VaporMoveBase unite && unite.IsExecuting && unite.RecoveryRemaining <= 0.18f) nearRecovery = true;
        if (!nearRecovery) return;
        vaporBufferedSlot = slot; vaporBufferReleased = false; vaporBufferUntil = Time.time + 0.22f;
    }
    private void ProcessVaporeonBufferedInput()
    {
        if (vaporBufferedSlot < 0) return;
        if (!IsLivingVaporeon || Time.time > vaporBufferUntil || controls.Movement.CancelMove.WasPressedThisFrame()
            || pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts)) { ClearVaporeonBufferedInput(); return; }
        int slot = vaporBufferedSlot;
        if (!moveStatuses[slot].HasStatus(ActionStatusType.None)) return;
        bool released = vaporBufferReleased; ClearVaporeonBufferedInput();
        moves[slot].Start(playerManager); if (released) moves[slot].Finish();
    }
    private void ClearVaporeonBufferedInput() { vaporBufferedSlot = -1; vaporBufferReleased = false; }
}
