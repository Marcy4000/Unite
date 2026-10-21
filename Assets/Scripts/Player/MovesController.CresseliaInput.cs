using UnityEngine;

public partial class MovesController
{
    private int cresseliaBufferedSlot = -1;
    private float cresseliaBufferUntil;
    private bool cresseliaBufferReleased;

    private void BufferCresseliaInput(int slot, bool released)
    {
        if (!IsLivingCresselia || playerManager.PlayerState != PlayerState.Alive) return;
        if (released)
        {
            if (cresseliaBufferedSlot == slot) cresseliaBufferReleased = true;
            return;
        }
        if (moveStatuses[slot].HasStatus(ActionStatusType.Stunned) || moveStatuses[slot].HasStatus(ActionStatusType.Cooldown)) return;
        bool nearRecovery = false;
        foreach (MoveBase move in moves)
            if (move is CresseliaMoveBase cast && cast.IsExecuting && cast.RecoveryRemaining <= 0.18f) nearRecovery = true;
        if (!nearRecovery) return;
        cresseliaBufferedSlot = slot; cresseliaBufferReleased = false; cresseliaBufferUntil = Time.time + 0.2f;
    }
    private void ProcessCresseliaBufferedInput()
    {
        if (cresseliaBufferedSlot < 0) return;
        if (!IsLivingCresselia || Time.time > cresseliaBufferUntil || controls.Movement.CancelMove.WasPressedThisFrame()
            || pokemon.HasAnyStatusEffect(CresseliaMoveRules.Interrupts)) { ClearCresseliaBufferedInput(); return; }
        int slot = cresseliaBufferedSlot;
        if (!moveStatuses[slot].HasStatus(ActionStatusType.None)) return;
        bool released = cresseliaBufferReleased;
        ClearCresseliaBufferedInput();
        moves[slot].Start(playerManager);
        if (released) moves[slot].Finish();
    }
    private void ClearCresseliaBufferedInput() { cresseliaBufferedSlot = -1; cresseliaBufferReleased = false; }
    private bool CanRecastCresseliaUnite => uniteMove is CresseliaUnite cresselia && cresselia.CanRecast
        && (uniteMoveStatus.GetStatus() & ~ActionStatusType.Cooldown) == ActionStatusType.None;
}
