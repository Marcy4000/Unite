using Unity.Netcode;
using UnityEngine;

public partial class Pokemon
{
    private static ushort nextMarshadowStolenId = 63000;

    // Called only by validated server hit resolution. Removing the donor buff
    // and installing its remaining lifetime happen in the same server operation.
    public int TransferMarshadowBuffs(Pokemon thief, bool upgraded)
    {
        if (!IsServer || thief == null || thief == this || thief.CurrentHp <= 0) return 0;
        int count = 0;
        for (int i = statChanges.Count - 1; i >= 0 && count < 8; i--)
        {
            StatChange original = statChanges[i];
            if (!MarshadowMoveRules.CanSteal(original, statTimers[i])) continue;
            StatChange copy = original;
            copy.Duration = MarshadowMoveRules.StolenDuration(statTimers[i], upgraded);
            // Reserve a namespaced, currently unused ID instead of stealing
            // ownership of another ability's refresh/cleanup identifier.
            int attempts = 0;
            do { nextMarshadowStolenId = (ushort)(nextMarshadowStolenId >= 63999 ? 63000 : nextMarshadowStolenId + 1); attempts++; }
            while (attempts < 1000 && thief.HasMarshadowStatId(nextMarshadowStolenId));
            if (thief.HasMarshadowStatId(nextMarshadowStolenId)) break;
            copy.ID = nextMarshadowStolenId;
            statChanges.RemoveAt(i); statTimers.RemoveAt(i);
            thief.AddStatChange(copy);
            count++;
        }
        return count;
    }

    private bool HasMarshadowStatId(ushort id)
    {
        foreach (StatChange change in statChanges) if (change.ID == id) return true;
        return false;
    }
}
