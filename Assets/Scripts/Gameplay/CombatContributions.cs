using System;
using System.Collections.Generic;

public static class AssistRules
{
    public const double Window = 10;
    public const int MaximumAssists = 4;
}

// One entry per player, not per hit/tick. Histories are local to the affected
// combatant and are never replicated; only the resolved KO and totals are sent.
public sealed class CombatContributionLedger
{
    public readonly struct Entry
    {
        public readonly ulong PlayerId;
        public readonly double Time;
        public readonly long Order;
        public Entry(ulong playerId, double time, long order) { PlayerId = playerId; Time = time; Order = order; }
    }

    private static long nextOrder;
    private readonly Dictionary<ulong, Entry> entries = new Dictionary<ulong, Entry>();
    private readonly List<ulong> expired = new List<ulong>();

    public void Record(ulong playerId, double now)
    {
        Prune(now);
        entries[playerId] = new Entry(playerId, now, ++nextOrder);
    }

    public List<Entry> Recent(double now)
    {
        Prune(now);
        var result = new List<Entry>(entries.Values);
        result.Sort(NewestFirst);
        return result;
    }

    public static int NewestFirst(Entry left, Entry right)
    {
        int time = right.Time.CompareTo(left.Time);
        return time != 0 ? time : right.Order.CompareTo(left.Order);
    }

    private void Prune(double now)
    {
        expired.Clear();
        foreach (var entry in entries)
            if (now - entry.Value.Time > AssistRules.Window || now < entry.Value.Time) expired.Add(entry.Key);
        foreach (ulong id in expired) entries.Remove(id);
    }

    public void Clear() => entries.Clear();
}

// Immutable server-side outcome, shared by stats, held items and presentation.
// DamageSource stays distinct from Killer when a wild Pokémon finishes a KO.
public sealed class KnockoutResult
{
    public Pokemon Victim { get; }
    public Pokemon DamageSource { get; }
    public Pokemon Killer { get; }
    public IReadOnlyList<Pokemon> Assistants { get; }
    public IReadOnlyList<ulong> AssistantIds { get; }
    public PokemonType VictimType { get; }
    public UnityEngine.Vector3 Position { get; }

    public KnockoutResult(Pokemon victim, Pokemon damageSource, Pokemon killer, Pokemon[] assistants)
    {
        Victim = victim;
        VictimType = victim.Type;
        Position = victim.transform.position;
        DamageSource = damageSource;
        Killer = killer;
        Assistants = Array.AsReadOnly((Pokemon[])assistants.Clone());
        var ids = new ulong[assistants.Length];
        for (int i = 0; i < ids.Length; i++) ids[i] = assistants[i].NetworkObjectId;
        AssistantIds = Array.AsReadOnly(ids);
    }
}
