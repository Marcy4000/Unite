using System;

public partial class PlayerManager
{
    private IDisposable battleItemStasisLock;
    private IDisposable battleItemStasisScoreLock;
    private IDisposable battleItemStasisRecallLock;

    private void SynchronizeBattleItemStasis()
    {
        bool active = pokemon != null && pokemon.CurrentHp > 0 && pokemon.HasStatusEffect(StatusType.BattleItemStasis);
        if (active && battleItemStasisLock == null) ApplyBattleItemStasis();
        else if (!active) RemoveBattleItemStasis();
    }

    private void ApplyBattleItemStasis()
    {
        ApplyStun();
        battleItemStasisLock = movesController.BattleItemStatus.AcquireStatus(ActionStatusType.Disabled);
        battleItemStasisScoreLock = scoreStatus.AcquireStatus(ActionStatusType.Disabled);
        battleItemStasisRecallLock = recallStatus.AcquireStatus(ActionStatusType.Disabled);
        movesController.BattleItem?.Cancel();
    }

    private void RemoveBattleItemStasis()
    {
        if (battleItemStasisLock == null) return;
        RemoveStun();
        battleItemStasisLock?.Dispose();
        battleItemStasisScoreLock?.Dispose();
        battleItemStasisRecallLock?.Dispose();
        battleItemStasisLock = null;
        battleItemStasisScoreLock = null;
        battleItemStasisRecallLock = null;
    }
}
