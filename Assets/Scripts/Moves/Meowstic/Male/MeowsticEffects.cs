using System;
using System.Collections.Generic;
using UnityEngine;

public static class MeowsticEffectIds
{
    // Reserved independently of Cinderace (15000+), Duraludon and Jolteon marks.
    private static readonly HashSet<ushort> used = new HashSet<ushort>();
    public static ushort Acquire()
    {
        for (ushort id = 10000; id < 14000; id++) if (used.Add(id)) return id;
        throw new InvalidOperationException("Meowstic effect ID pool exhausted.");
    }
    public static void Release(ushort id) { used.Remove(id); }
}

// Strongest-wins within one move family. Each source releases only its own
// contribution; overlapping rooms/coats/Unites never erase another caster's buff.
public sealed class MeowsticModifier : IDisposable
{
    private sealed class Group
    {
        public readonly ushort Id = MeowsticEffectIds.Acquire();
        public readonly List<MeowsticModifier> Sources = new List<MeowsticModifier>();
    }
    private static readonly Dictionary<(Pokemon, Stat, bool, bool, int, float), Group> groups = new Dictionary<(Pokemon, Stat, bool, bool, int, float), Group>();
    private readonly (Pokemon, Stat, bool, bool, int, float) key;
    private Group group;
    private short amount;
    public short EffectiveAmount
    {
        get { short maximum = 0; if (group != null) foreach (var source in group.Sources) maximum = (short)Mathf.Max(maximum, source.amount); return maximum; }
    }
    public MeowsticModifier(Pokemon target, Stat stat, short amount, bool buff = true, bool percentage = true, int family = 0, float duration = 0)
    {
        key = (target, stat, buff, percentage, family, duration); this.amount = amount;
        if (!groups.TryGetValue(key, out group)) { group = new Group(); groups.Add(key, group); }
        group.Sources.Add(this); Rebuild();
    }
    public void SetAmount(short value)
    {
        if (group == null) return;
        bool missing = value > 0 && key.Item1 != null && key.Item1.IsSpawned && key.Item1.IsServer && !key.Item1.HasStatChangeWithID(group.Id);
        if (amount == value && !missing) return;
        amount = value; Rebuild();
    }
    public void RefreshAmount(short value) { if (group == null) return; amount = value; Rebuild(); }
    public void Rebuild()
    {
        if (group == null || key.Item1 == null || !key.Item1.IsSpawned || !key.Item1.IsServer) return;
        key.Item1.RemoveAllStatChangeWithIDRPC(group.Id);
        if (EffectiveAmount > 0 && key.Item1.CurrentHp > 0)
            key.Item1.AddStatChange(new StatChange(EffectiveAmount, key.Item2, key.Item6, key.Item6 > 0, key.Item3, key.Item4, group.Id));
    }
    public void Dispose()
    {
        if (group == null) return;
        group.Sources.Remove(this); Rebuild();
        if (group.Sources.Count == 0) { groups.Remove(key); MeowsticEffectIds.Release(group.Id); }
        group = null;
    }
}

public sealed class MeowsticShield : IDisposable
{
    private Pokemon target;
    public ushort Id { get; private set; }
    public MeowsticShield(Pokemon target, float duration)
    {
        this.target = target; Id = MeowsticEffectIds.Acquire();
        target.AddShieldRPC(new ShieldInfo(Mathf.RoundToInt(target.GetMaxHp() * 0.25f), Id, 0, duration, true));
    }
    public void Dispose()
    {
        if (Id == 0) return;
        if (target != null && target.IsSpawned && target.IsServer) target.RemoveShieldWithIDRPC(Id);
        MeowsticEffectIds.Release(Id); Id = 0; target = null;
    }
}

public sealed class MeowsticCleanup : IDisposable
{
    private Action action;
    public MeowsticCleanup(Action action) { this.action = action; }
    public void Dispose() { Action cleanup = action; action = null; cleanup?.Invoke(); }
}
