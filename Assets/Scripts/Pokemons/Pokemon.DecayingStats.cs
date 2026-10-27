using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class Pokemon
{
    private readonly Dictionary<ushort, int> decayingStatVersions = new Dictionary<ushort, int>();

    /// <summary>A refreshing debuff with a weaker second phase. Runs on its recipient.</summary>
    [Rpc(SendTo.Server)]
    public void AddDecayingStatRPC(StatChange change, short finalAmount, float decayAfter)
    {
        int version = decayingStatVersions.TryGetValue(change.ID, out int old) ? old + 1 : 1;
        decayingStatVersions[change.ID] = version;
        RemoveAllStatChangeWithIDRPC(change.ID);
        AddStatChange(change);
        StartCoroutine(DecayStat(change, finalAmount, decayAfter, version));
    }

    private IEnumerator DecayStat(StatChange change, short finalAmount, float after, int version)
    {
        yield return new WaitForSeconds(Mathf.Max(0, after));
        if (!IsSpawned || CurrentHp <= 0 || decayingStatVersions[change.ID] != version) yield break;
        bool present = false;
        foreach (StatChange current in statChanges) if (current.Equals(change)) { present = true; break; }
        // Cleansing during the first phase must not reapply the debuff.
        if (!present) yield break;
        RemoveAllStatChangeWithIDRPC(change.ID);
        change.Amount = finalAmount;
        change.Duration = Mathf.Max(0, change.Duration - after);
        if (change.Duration > 0) AddStatChange(change);
    }

    /// <summary>Multi-step decay; cleansing never recreates a removed debuff.</summary>
    [Rpc(SendTo.Server)]
    public void AddSteppedDecayingStatRPC(StatChange change, short decrement, float interval)
    {
        if (!change.IsTimed || change.Duration <= 0 || decrement <= 0 || interval <= 0) return;
        int version = decayingStatVersions.TryGetValue(change.ID, out int previous) ? previous + 1 : 1;
        decayingStatVersions[change.ID] = version;
        RemoveAllStatChangeWithIDRPC(change.ID); AddStatChange(change);
        StartCoroutine(SteppedDecay(change, decrement, interval, version));
    }

    private IEnumerator SteppedDecay(StatChange change, short decrement, float interval, int version)
    {
        float started = Time.time, next = started + interval, end = started + change.Duration;
        while (Time.time < end)
        {
            yield return null;
            if (!IsSpawned || CurrentHp <= 0 || decayingStatVersions[change.ID] != version) yield break;
            bool present = false;
            foreach (var current in statChanges) if (current.Equals(change)) { present = true; break; }
            if (!present) yield break;
            if (Time.time < next || Time.time >= end) continue;
            int steps = Mathf.FloorToInt((Time.time - next) / interval) + 1; next += steps * interval;
            RemoveAllStatChangeWithIDRPC(change.ID);
            change.Amount = (short)Mathf.Max(0, change.Amount - decrement * steps);
            change.Duration = end - Time.time;
            if (change.Amount <= 0) yield break;
            AddStatChange(change);
        }
    }
}
