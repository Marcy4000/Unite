using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>World units = databin millimetres / 1000. Unspecified values are deliberately tunable here.</summary>
public static class EspeonMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/Espeon/";
    public const string Visuals = "Effects/Espeon/";
    public static float BasicRange => TargetRange;
    public static float BeamLength = 7.5f, BeamWidth = 1, BoostRecharge = 5;
    public static float FocusRange = 9, FocusRadius = 1.5f, ProjectileDistance = 10;
    public static float ProjectileSpacing = 1, ProjectileRadius = 0.3f, ProjectileSpeed = 18, UpgradedProjectileSpeed = 24;
    public static float TargetRange = 7.5f, StoredRange = 7.5f, StoredSpeed = 14, StoredInterval = 0.08f;
    public static float StoredRecharge = 2, StoredHasteDuration = 1.2f;
    public const int MaxBlasts = 5, EmpoweredBlasts = 8;
    public static float PsybeamRange = 9, PsybeamWidth = 1.7f, SplitRangeRatio = 0.75f;
    public static float FutureDelay = 2, FutureRadius = 2, BonusWindow = 5;
    public static float FutureCooldownReduction = 0.8f;
    public static float UniteRadius = 6, UniteCaptureAt = 0.2f, UniteExplodeAt = 1.4f, UniteRecovery = 1.7f;
    public static float UniteShove = 2, UniteShoveDuration = 0.25f, LiftHeight = 1.2f;
    public static float PassiveCooldown = 35, PassiveImmunity = 0.45f;
    public static float BasicWindup = 0.2f, BasicRecovery = 0.15f, InputBuffer = 0.15f;

    public static bool Living(Pokemon p) => p != null && p.IsSpawned && p.CurrentHp > 0;
    public static bool CanHit(PlayerManager actor, Pokemon target, bool acquire = false) => actor != null && Living(target)
        && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam) && !target.HasStatusEffect(StatusType.Invincible)
        && !target.HasStatusEffect(StatusType.Untargetable) && (!acquire || !target.HasStatusEffect(StatusType.Invisible));
    public static string Animation(PlayerManager actor, string clip) => actor.Pokemon.CurrentLevel >= 3
        ? "ani_" + clip + "_bat_019600" : EeveeAnimation(clip);
    public static string EeveeAnimation(string clip) => "ani_" + (clip == "spell1" ? "spell1a" : clip) + "_bat_0133";
    public static bool MoveOne(AvailableMoves id) => id == AvailableMoves.EspeonPsyshock || id == AvailableMoves.EspeonStoredPower;
    public static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
    public static Vector3 ProjectileOrigin(Vector3 origin, Vector3 facing, int index, int count)
        => origin + Vector3.Cross(Vector3.up, facing.normalized) * ((index - (count - 1) * 0.5f) * ProjectileSpacing);
    public static bool InSplitFan(Vector3 from, Vector3 target, Vector3 facing) => Vector3.Dot(Flat(target - from), Flat(facing)) >= 0;
    public static void Shove(Pokemon target, Vector3 direction)
    {
        if (target.HasStatusEffect(StatusType.Unstoppable) || target.HasStatusEffect(StatusType.HindranceResistance)) return;
        if (target.TryGetComponent(out PlayerMovement movement)) movement.DisplaceRPC(direction, UniteShove, UniteShoveDuration);
        else if (target.TryGetComponent(out WildPokemonAI wild)) wild.TryDisplace(direction, UniteShove, UniteShoveDuration);
        else target.ApplyKnockbackRPC(direction, UniteShove / Mathf.Max(0.01f, UniteShoveDuration));
    }
    public static DamageInfo Basic(ulong source, bool boosted, bool evolved) => !boosted
        ? DamageInfo.Tuned("espeon.basic", new DamageInfo(source, 1, 0, 0, DamageType.Physical, DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit))
        : DamageInfo.Tuned("espeon.basic.2", new DamageInfo(source, evolved ? 0.54f : 0.35f, (short)(evolved ? 12 : 10), (short)(evolved ? 230 : 180),
            DamageType.Special, DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit));
    public static DamageInfo Volley(ulong source, bool psyshock, bool repeat) => psyshock
        ? DamageInfo.Tuned("espeon.volley", new DamageInfo(source, repeat ? 0.22f : 0.726f, (short)(repeat ? 4 : 14), (short)(repeat ? 112 : 374), DamageType.Special))
        : DamageInfo.Tuned("espeon.volley.2", new DamageInfo(source, repeat ? 0.21f : 0.7f, (short)(repeat ? 6 : 19), (short)(repeat ? 108 : 360), DamageType.Special));
    public static DamageInfo Stored(ulong source, bool repeat) => DamageInfo.Tuned("espeon.stored", new DamageInfo(source, repeat ? 0.196f : 0.6f,
        (short)(repeat ? 2 : 9), (short)(repeat ? 90 : 300), DamageType.Special));
    public static DamageInfo Growl(ulong source) => DamageInfo.Tuned("espeon.growl", new DamageInfo(source, 0.55f, 15, 290, DamageType.Special));
    public static DamageInfo Counter(ulong source) => DamageInfo.Tuned("espeon.counter", new DamageInfo(source, 0.34f, 8, 140, DamageType.Special) { source = DamageSource.Secondary });
    public static DamageInfo Psybeam(ulong source) => DamageInfo.Tuned("espeon.psybeam", new DamageInfo(source, 1, 14, 591, DamageType.Special));
    public static DamageInfo Future(ulong source) => DamageInfo.Tuned("espeon.future", new DamageInfo(source, 0.576f, 9, 338, DamageType.Special));
    public static DamageInfo Unite(ulong source) => DamageInfo.Tuned("espeon.unite", new DamageInfo(source, 0.91f, 12, 546, DamageType.Special, DamageProprieties.IsUniteMove));
    public static bool TriggersPassive(StatusEffect effect) => effect.IsNegativeStatus()
        || effect.Type == StatusType.Burned || effect.Type == StatusType.Poisoned;
    public static bool TriggersPassive(StatChange change) => !change.IsBuff && change.AffectedStat == Stat.Speed;
}

/// <summary>Blasts are ammunition, not stored move uses. Their 2s generation ignores CDR.</summary>
public sealed class EspeonBlasts
{
    public int Count { get; private set; }
    private double next;
    public void Reset(double now) { Count = 0; next = now + EspeonMoveRules.StoredRecharge; }
    public void Tick(double now)
    {
        double interval = Math.Max(0.01, EspeonMoveRules.StoredRecharge);
        if (now < next) return;
        int generated = (int)Math.Min(EspeonMoveRules.MaxBlasts, 1 + Math.Floor((now - next) / interval));
        Count = Math.Min(EspeonMoveRules.MaxBlasts, Count + generated);
        next = now + interval - (now - next) % interval;
    }
    public void Add(int amount) { Count = Mathf.Clamp(Count + amount, 0, EspeonMoveRules.MaxBlasts); }
    public int Spend() { int result = Count; Count = 0; return result; }
}

public static class EspeonEffectIds
{
    private static readonly HashSet<ushort> used = new HashSet<ushort>();
    private static readonly Dictionary<ushort, float> pending = new Dictionary<ushort, float>();
    public static ushort Acquire()
    {
        foreach (var pair in new List<KeyValuePair<ushort, float>>(pending))
            if (Time.time >= pair.Value) { pending.Remove(pair.Key); used.Remove(pair.Key); }
        for (ushort id = 36000; id < 38000; id++) if (used.Add(id)) return id;
        throw new InvalidOperationException("Espeon effect IDs exhausted.");
    }
    public static void Release(ushort id) { used.Remove(id); }
    public static void ReleaseAfter(ushort id, float duration) { pending[id] = Time.time + duration; }
}
