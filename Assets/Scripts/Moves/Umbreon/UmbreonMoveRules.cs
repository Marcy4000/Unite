using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Unite-DB formulas; dump distances / 1000. Unverified geometry is deliberately editable.</summary>
public static class UmbreonMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Umbreon/";
    public const string Visuals = "Effects/Umbreon/";
    public static float BasicRange = 2.5f, BasicRadius = 1, BasicWindup = 0.2f, BasicRecovery = 0.18f;
    public static float SwiftRange = 6, SwiftProximity = 3, SwiftSpeed = 18, SwiftInterval = 0.1f, SwiftMarkDuration = 4;
    public static float TearsRadius = 4, MeanRange = 4, MeanRadius = 2, MeanDelay = 0.5f;
    public static float DashDistance = 4.5f, SecondDashDistance = 3.5f, DashDuration = 0.25f, DashRadius = 0.75f;
    public static float TailRadius = 2.5f, TailAngle = 120, ShoveDistance = 1.5f, ShoveDuration = 0.6f, RecastWindow = 6;
    public static float WishRange = 7, WishTransferRadius = 10, WishDelay = 2;
    public static float SnarlRadius = 4, SnarlDuration = 1.5f;
    public static int SnarlTicks = 8;
    public static float UniteRadius = 4, UniteAuraRadius = 4.5f, UniteLeap = 0.86f, UniteAuraDuration = 5;
    public static float UniteMoveMultiplier = 0.5f, StolenShieldTail = 3, PassiveCooldown = 40, PassiveImmunity = 1.2f;
    public static float MeanDuration(bool plus) => plus ? 3.5f : 3;
    public static bool Living(Pokemon p) => p != null && p.IsSpawned && p.CurrentHp > 0;
    public static bool Enemy(PlayerManager actor, Pokemon target, bool acquire = false) => actor != null && Living(target)
        && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam) && !target.HasStatusEffect(StatusType.Invincible)
        && !target.HasStatusEffect(StatusType.Untargetable) && (!acquire || !target.HasStatusEffect(StatusType.Invisible));
    public static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    public static bool InCircle(Vector3 center, Vector3 position, float radius) => Flat(position - center).sqrMagnitude <= radius * radius;
    public static string Animation(PlayerManager actor, string clip) => actor.Pokemon.CurrentLevel >= 3 ? "ani_" + clip + "_bat_0197" : EeveeAnimation(clip);
    public static string EeveeAnimation(string clip) => "ani_" + (clip == "spell1" ? "spell1a" : clip) + "_bat_0133";
    public static bool TriggersPassive(StatusEffect effect) => effect.Type != StatusType.Bound &&
        (EspeonMoveRules.TriggersPassive(effect) || effect.Type == StatusType.UmbreonMeanLookPending || effect.Type == StatusType.UmbreonSwiftMarked);
    public static bool TriggersPassive(StatChange change) => EspeonMoveRules.TriggersPassive(change);
    public static bool Stealable(ShieldInfo shield) => shield.Amount > 0 && shield.ID != RayquazaBuff.ShieldId
        && shield.ID != HeldItemRules.AssaultVestId;
    public static int WishHeal(int attack, int level, bool plus) => Mathf.FloorToInt((plus ? 3.8f : 3) * attack + 18 * level + (plus ? 700 : 600));
    public static int FoulFollowup(int recordedAttack, int ownAttackAtFirstHit, int currentAttack)
        => Mathf.FloorToInt(2.376f * (recordedAttack > ownAttackAtFirstHit ? recordedAttack : currentAttack));
    public static DamageInfo Damage(ulong source, float ratio, short slider, short value) => new DamageInfo(source, ratio, slider, value, DamageType.Physical);
    public static DamageInfo Basic(ulong source, bool boosted, bool mean) => DamageInfo.Tuned("umbreon.basic", new DamageInfo(source,
        mean ? 1.96f : boosted ? 1.4f : 1, (short)(mean ? 5 : 0), (short)(mean ? 180 : 0), DamageType.Physical,
        DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit));
    public static DamageInfo Swift(ulong source, bool first) => DamageInfo.Tuned("umbreon.swift", Damage(source, first ? 0.4f : 0.12f, (short)(first ? 10 : 2), (short)(first ? 100 : 30)));
    public static DamageInfo Tears(ulong source) => DamageInfo.Tuned("umbreon.tears", Damage(source, 0.86f, 8, 240));
    public static DamageInfo Foul(ulong source) => DamageInfo.Tuned("umbreon.foul", Damage(source, 0.58f, 6, 145));
    public static DamageInfo Snarl(ulong source, bool final) => DamageInfo.Tuned("umbreon.snarl", Damage(source, final ? 0.3f : 0.15f, (short)(final ? 2 : 1), (short)(final ? 80 : 40)));
    public static DamageInfo Unite(ulong source) => DamageInfo.Tuned("umbreon.unite", new DamageInfo(source, 2.24f, 13, 630, DamageType.Physical, DamageProprieties.IsUniteMove));
    public static Pokemon LowestAlly(IEnumerable<Pokemon> candidates, Pokemon previous, Vector3 center, float radius)
    {
        Pokemon best = null; float hp = float.MaxValue;
        foreach (var p in candidates)
        {
            if (p == previous || !Living(p) || p.Type != PokemonType.Player || !p.TeamMember.IsOnSameTeam(previous.TeamMember)
                || !InCircle(center, p.transform.position, radius)) continue;
            float fraction = (float)p.CurrentHp / Mathf.Max(1, p.GetMaxHp());
            if (fraction < hp || (Mathf.Approximately(fraction, hp) && (best == null || p.NetworkObjectId < best.NetworkObjectId))) { hp = fraction; best = p; }
        }
        return best;
    }
}

public static class UmbreonEffectIds
{
    private static readonly HashSet<ushort> used = new HashSet<ushort>();
    private static readonly Dictionary<ushort, float> pending = new Dictionary<ushort, float>();
    private static readonly HashSet<ushort> stolen = new HashSet<ushort>();
    public static ushort Acquire(bool shield = false)
    {
        foreach (var pair in new List<KeyValuePair<ushort, float>>(pending))
            if (Time.time >= pair.Value) { pending.Remove(pair.Key); used.Remove(pair.Key); stolen.Remove(pair.Key); }
        for (ushort id = 38000; id < 40000; id++) if (used.Add(id)) { if (shield) stolen.Add(id); return id; }
        throw new InvalidOperationException("Umbreon effect IDs exhausted.");
    }
    public static bool IsStolenShield(ushort id) => stolen.Contains(id);
    public static void Release(ushort id, float delay = 0) { if (id == 0) return; if (delay > 0) pending[id] = Time.time + delay; else { used.Remove(id); stolen.Remove(id); } }
}
