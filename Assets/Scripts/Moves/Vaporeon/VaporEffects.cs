using System;
using System.Collections.Generic;
using UnityEngine;

public static class VaporEffectIds
{
    // Jolteon's status-mark pool ends at 59999; Marshadow starts at 62000.
    public const ushort First = 60000, Last = 61999;
    private static readonly HashSet<ushort> used = new HashSet<ushort>();
    public static ushort Acquire()
    {
        for (ushort id = First; id <= Last; id++) if (used.Add(id)) return id;
        throw new InvalidOperationException("Vaporeon effect ID pool exhausted.");
    }
    public static void Release(ushort id) { used.Remove(id); }
}

// A source-owned contribution to a strongest-wins modifier. Leaving one field
// cannot clear another field, and two Whirlpools/Rains never add their slows.
public sealed class VaporModifierLease : IDisposable
{
    private sealed class Group
    {
        public readonly ushort Id = VaporEffectIds.Acquire();
        public readonly List<VaporModifierLease> Sources = new List<VaporModifierLease>();
    }
    private static readonly Dictionary<(Pokemon, Stat, bool), Group> groups = new Dictionary<(Pokemon, Stat, bool), Group>();
    private readonly (Pokemon, Stat, bool) key;
    private readonly short amount;
    private Group group;
    public short EffectiveAmount
    {
        get
        {
            short maximum = 0;
            if (group != null) foreach (var source in group.Sources) maximum = (short)Mathf.Max(maximum, source.amount);
            return maximum;
        }
    }
    public VaporModifierLease(Pokemon target, Stat stat, short amount, bool buff)
    {
        key = (target, stat, buff); this.amount = amount;
        if (!groups.TryGetValue(key, out group)) { group = new Group(); groups.Add(key, group); }
        group.Sources.Add(this); Rebuild();
    }
    private void Rebuild()
    {
        if (key.Item1 == null || !key.Item1.IsSpawned || !key.Item1.IsServer) return;
        key.Item1.RemoveAllStatChangeWithIDRPC(group.Id);
        short maximum = EffectiveAmount;
        if (maximum > 0 && key.Item1.CurrentHp > 0)
            key.Item1.AddStatChange(new StatChange(maximum, key.Item2, 0, false, key.Item3, true, group.Id));
    }
    public void Dispose()
    {
        if (group == null) return;
        group.Sources.Remove(this); Rebuild();
        if (group.Sources.Count == 0) { groups.Remove(key); VaporEffectIds.Release(group.Id); }
        group = null;
    }
}

public sealed class VaporShieldLease : IDisposable
{
    private Pokemon target;
    public ushort Id { get; private set; }
    public bool Intact => VaporMoveRuntime.Living(target) && target.HasShieldWithID(Id);
    public VaporShieldLease(Pokemon target, int amount)
    {
        this.target = target; Id = VaporEffectIds.Acquire();
        target.OnDeath += OnDeath;
        target.AddShieldRPC(new ShieldInfo(amount, Id, 1, VaporMoveRules.RingDuration, false));
    }
    private void OnDeath(DamageInfo damage) { Dispose(); }
    public void Dispose()
    {
        if (Id == 0) return;
        if (target != null)
        {
            target.OnDeath -= OnDeath;
            if (target.IsSpawned && target.IsServer) target.RemoveShieldWithIDRPC(Id);
        }
        VaporEffectIds.Release(Id); Id = 0; target = null;
    }
}
