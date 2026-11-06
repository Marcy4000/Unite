using System.Collections.Generic;
using UnityEngine;

// Unite DB (pokemon.json, 2026-11-06). Icicle Spear intentionally retains the custom cone rework.
public static class GlaceonMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Glaceon/";
    public const float BasicRange = 7, WindRange = 8, CrystalLifetime = 5, InputBuffer = 0.2f;
    public const int MaxCrystals = 8;
    public const ushort SpearSlow = 22500, ShardSpeed = 22501, ShardAttackSpeed = 22502, FreezeAttack = 22503;
    public const ushort UniteSpeed = 22504, UniteAttackSpeed = 22505, UniteShield = 22506, UniteLeap = 22507, ZoneSpeed = 22508, Cloak = 22509;
    public static readonly StatusType[] Interrupts = { StatusType.Incapacitated, StatusType.Asleep, StatusType.Frozen, StatusType.Bound };
    public static bool Living(Pokemon target) => target != null && target.IsSpawned && target.CurrentHp > 0;
    public static bool Evolved(PlayerManager actor) => actor.Pokemon.CurrentLevel >= 3;
    public static Vector3 Direction(Vector3 direction, Vector3 fallback)
    {
        direction.y = fallback.y = 0;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : fallback.sqrMagnitude > 0.001f ? fallback.normalized : Vector3.forward;
    }
    public static bool CanHit(PlayerManager actor, Pokemon target) => actor != null && Living(target)
        && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam) && !target.HasStatusEffect(StatusType.Invincible)
        && !target.HasStatusEffect(StatusType.Untargetable);
    public static HashSet<Pokemon> Targets(Collider[] colliders)
    {
        var targets = new HashSet<Pokemon>();
        foreach (var collider in colliders)
        {
            var target = collider.GetComponentInParent<Pokemon>();
            if (target != null) targets.Add(target);
        }
        return targets;
    }
    public static float Cooldown(AvailableMoves move)
    {
        switch (move)
        {
            case AvailableMoves.GlaceTailWhip: return 8;
            case AvailableMoves.GlaceIceShard: return 8.5f;
            case AvailableMoves.GlaceFreezeDry: return 7.5f;
            case AvailableMoves.GlaceIcyWind: return 6.5f;
            case AvailableMoves.GlaceonUnite: return 0;
            default: return 5;
        }
    }
    public static DamageInfo Basic(ulong source, bool boosted, bool evolved) => new DamageInfo(source,
        boosted ? evolved ? 0.47f : 0.4485f : 0.39f, (short)(boosted ? 5 : 4), (short)(boosted ? evolved ? 90 : 81 : 70),
        DamageType.Special, DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit);
    public static DamageInfo Damage(ulong source, AvailableMoves move)
    {
        switch (move)
        {
            case AvailableMoves.GlaceSwift: return new DamageInfo(source, 0.16f, 6, 110, DamageType.Special);
            case AvailableMoves.GlaceTailWhip: return new DamageInfo(source, 0.61f, 6, 190, DamageType.Special);
            case AvailableMoves.GlaceIceShard: return new DamageInfo(source, 0.52f, 6, 100, DamageType.Special, DamageProprieties.CanCrit | DamageProprieties.IsBasicAttack);
            case AvailableMoves.GlaceIcyWind: return new DamageInfo(source, 0.20f, 2, 35, DamageType.Special, DamageProprieties.CanCrit);
            case AvailableMoves.GlaceFreezeDry: return new DamageInfo(source, 0.71f, 5, 226, DamageType.Special);
            case AvailableMoves.GlaceonUnite: return new DamageInfo(source, 1.21f, 8, 384, DamageType.Special, DamageProprieties.IsUniteMove);
            default: return new DamageInfo(source, 0.64f, 6, 130, DamageType.Special);
        }
    }
    public static int WindSlow(int hits) => hits < 4 ? 0 : Mathf.Min(hits, 6) * 8;
    public static short WindExtraDamage(int remainingHp, bool wild) => (short)Mathf.Clamp(Mathf.FloorToInt(remainingHp * 0.015f), 0, wild ? 300 : short.MaxValue);
}

// Pure resource transitions: delayed returns add to new gains and cannot overwrite them.
public sealed class GlaceonCrystalState
{
    public int Count { get; private set; }
    public double ExpiresAt { get; private set; }
    public void Tick(double now) { if (now >= ExpiresAt) Count = 0; }
    public void Add(int count, double now)
    {
        Tick(now); Count = Mathf.Clamp(Count + Mathf.Max(0, count), 0, GlaceonMoveRules.MaxCrystals);
        if (Count > 0) ExpiresAt = now + GlaceonMoveRules.CrystalLifetime;
    }
    public void Refresh(double now) { Tick(now); if (Count > 0) ExpiresAt = now + GlaceonMoveRules.CrystalLifetime; }
    public int TakeAll(double now) { Tick(now); int count = Count; Clear(); return count; }
    public void Consume(double now) { Refresh(now); Count = Mathf.Max(0, Count - 1); }
    public void Clear() { Count = 0; ExpiresAt = 0; }
}

public static class GlaceonEffectIds
{
    private static readonly HashSet<ushort> used = new HashSet<ushort>();
    private static readonly Dictionary<ushort, float> pending = new Dictionary<ushort, float>();
    public static ushort Acquire()
    {
        foreach (var pair in new List<KeyValuePair<ushort, float>>(pending))
            if (Time.time >= pair.Value) { pending.Remove(pair.Key); used.Remove(pair.Key); }
        for (ushort id = 40000; id < 42000; id++) if (used.Add(id)) return id;
        throw new System.InvalidOperationException("Glaceon effect IDs exhausted.");
    }
    public static void Release(ushort id) { used.Remove(id); }
    public static void ReleaseAfter(ushort id, float duration) { pending[id] = Time.time + duration; }
}
