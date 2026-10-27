using UnityEngine;

public partial class MovesController
{
    private readonly BufferedMoveInput serverAttackInput = new BufferedMoveInput();
    private MoveBase serverAttackBufferedMove;
    private bool serverAttackPresented, serverAttackPending;
    private float serverAttackRequestUntil;
    private float ServerAttackBufferDuration => basicAttack?.MoveInputBufferDuration ?? 0;
    public bool IsServerBasicAttackAnimating => serverAttackPresented || (serverAttackPending && Time.time <= serverAttackRequestUntil);

    public void RequestServerBasicAttack(ulong targetId)
    {
        serverAttackPending = true;
        serverAttackRequestUntil = Time.time + 1f;
        PerformServerBasicAttackRPC(targetId);
    }

    private bool BufferMoveDuringServerAttack(int slot, bool released)
    {
        if (ServerAttackBufferDuration <= 0) return false;
        if (!IsServerBasicAttackAnimating)
        {
            // A fresh press supersedes an older queued intention.
            if (!released) ClearServerAttackBufferedInput();
            return false;
        }
        var status = slot == 2 ? uniteMoveStatus : moveStatuses[slot];
        if (pokemon == null || pokemon.CurrentHp <= 0 || pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions)
            || status == null || !status.HasStatus(ActionStatusType.None))
        { ClearServerAttackBufferedInput(); return true; }
        if (serverAttackInput.Queue(slot, released, Time.time, ServerAttackBufferDuration))
            serverAttackBufferedMove = slot == 2 ? uniteMove : moves[slot];
        return true;
    }

    private void ProcessServerAttackBufferedInput()
    {
        if (!serverAttackInput.Pending) return;
        if (!serverAttackInput.IsValid(Time.time) || pokemon == null || pokemon.CurrentHp <= 0
            || pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions))
        { ClearServerAttackBufferedInput(); return; }
        if (IsServerBasicAttackAnimating) return;
        int slot = serverAttackInput.Slot;
        var move = slot == 2 ? uniteMove : moves[slot];
        var status = slot == 2 ? uniteMoveStatus : moveStatuses[slot];
        if (move != serverAttackBufferedMove) { ClearServerAttackBufferedInput(); return; }
        if (status == null || !status.HasStatus(ActionStatusType.None)) return;
        bool released = serverAttackInput.Released;
        ClearServerAttackBufferedInput();
        move.Start(playerManager);
        if (released) move.Finish();
    }

    private bool ServerAttackInputHasPriority()
    {
        if (ServerAttackBufferDuration <= 0) return false;
        if (!serverAttackInput.IsValid(Time.time)) ClearServerAttackBufferedInput();
        return serverAttackInput.Pending || IsServerBasicAttackAnimating
            || (moves[0]?.IsActive ?? false) || (moves[1]?.IsActive ?? false) || (uniteMove?.IsActive ?? false);
    }

    private void ClearServerAttackBufferedInput()
    {
        serverAttackInput.Clear(); serverAttackBufferedMove = null;
    }

    private void ResetServerAttackInput()
    {
        ClearServerAttackBufferedInput(); serverAttackPending = serverAttackPresented = false;
        serverAttackRequestUntil = 0;
    }
}
