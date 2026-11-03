using UnityEngine;
using System;
using Unity.Netcode;

public partial class Pokemon
{
    public event Action OnBattleItemStatusChange;

    private void OnBattleItemStatusListChanged(NetworkListEvent<StatusEffect> change)
    {
        if (change.Type == NetworkListEvent<StatusEffect>.EventType.Clear
            || BattleItemMovementStatus(change.Value.Type) || BattleItemMovementStatus(change.PreviousValue.Type))
            OnBattleItemStatusChange?.Invoke();
    }

    private static bool BattleItemMovementStatus(StatusType status)
        => status == StatusType.SpeedReductionImmunity || status == StatusType.BattleItemStasis;

    public void CleanseBattleItemConditions()
    {
        if (!IsServer) return;
        foreach (var effect in statusEffects.AsNativeArray().ToArray())
            if (effect.IsNegativeStatus() || effect.Type == StatusType.Burned || effect.Type == StatusType.Poisoned)
                RemoveStatusEffectRPC(effect);
        foreach (var change in statChanges.AsNativeArray().ToArray())
            if (!change.IsBuff && change.CanBeRemoved) RemoveStatChangeRPC(change);
    }

    public void RemoveBattleItemModifiers(ushort id, ulong source)
    {
        if (!IsServer) return;
        for (int index = statChanges.Count - 1; index >= 0; index--)
            if (statChanges[index].ID == id && statChanges[index].HasSource && statChanges[index].SourceId == source)
                RemoveStatAt(index);
    }

    public bool HasBattleItemModifier(ushort id, ulong source, Stat stat)
    {
        foreach (var change in statChanges)
            if (change.ID == id && change.HasSource && change.SourceId == source && change.AffectedStat == stat) return true;
        return false;
    }
}
