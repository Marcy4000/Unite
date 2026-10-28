using UnityEngine;

public partial class BotBrain
{
    private MoveBase pendingMove;
    private int pendingSlot;
    private float releaseAt;

    private void BeginBotMove(int slot)
    {
        var moves = player.MovesController;
        if (slot == 2) moves.TryUsingUniteMove(); else moves.TryUsingMove(slot);
        var move = moves.GetMove(slot == 2 ? MoveType.UniteMove : slot == 0 ? MoveType.MoveA : MoveType.MoveB);
        if (move == null) return;
        if (!move.IsActive)
        {
            // Buffered input still needs its release edge.
            if (slot == 2) moves.TryFinishingUniteMove(); else moves.TryFinishingMove(slot);
            return;
        }
        pendingMove = move; pendingSlot = slot;
        // Let ordinary Update capture aim/load effects before releasing. Charged
        // and held moves keep their normal timed auto-finish behavior.
        releaseAt = Time.time + (move is GlaceIcicleSpear ? 10 : move is DuraDragonPulse ? 1.2f : .15f);
    }

    private void TickMoveRelease()
    {
        if (pendingMove == null) return;
        if (!pendingMove.IsActive) { pendingMove = null; return; }
        if (State == BotState.Retreat || State == BotState.Recover || State == BotState.Recall)
        {
            pendingMove.Cancel(); pendingMove = null; return;
        }
        if (Time.time < releaseAt) return;
        if (pendingSlot == 2) player.MovesController.TryFinishingUniteMove();
        else player.MovesController.TryFinishingMove(pendingSlot);
        pendingMove = null;
    }
}
