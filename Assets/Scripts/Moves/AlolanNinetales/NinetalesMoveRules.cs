using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AGE-backed horizontal geometry (dump distances / 1000). Durations, speeds and
/// formulas remain project tuning: age-named-v2 does not establish runtime time units.
/// Damage sliders use zero-based CurrentLevel.
/// </summary>
public static class NinetalesMoveRules
{
    public const string Folder = "Assets/Prefabs/Objects/Moves/AlolanNinetales/";
    public const string Visuals = "Effects/AlolanNinetales/";
    public const float BasicRange = 7, BasicSpeed = 20;
    // A1B2/A1B4: forward-offset 3000 x 4000 cube, not a radial splash.
    public const float BoostedWidth = 3, BoostedLength = 4, BoostedForwardOffset = 1.5f;
    public const float BasicWindup = 0.20f, BasicRecovery = 0.20f, BasicInputBuffer = 0.18f;
    public const float BasicReferenceCooldown = 1f, AnimationBlend = 0.06f;
    public const float PassiveRange = 7, PassiveCooldown = 4, SnowDuration = 4, FreezeDuration = 1;
    public const float PassiveWarningDuration = 1, PassiveRadius = 2; // P1B1 MRadius=2000
    public const int FreezeStacks = 4;
    public const float ConeRange = 7, ConeAngle = 60, ConeWindup = 0.15f, ConeRecovery = 0.16f;
    public const float AvalancheRange = 6.5f, AvalancheWidth = 4, AvalancheGustLength = 5, WallWidth = 4, WallThickness = 0.6f;
    public const float WallHeight = 2.5f, WallLifetime = 2, CollapseRadius = 2.5f;
    public const float CollapseStartRadius = 1, CollapseDuration = 0.25f;
    public const float AvalancheWindup = 0.15f, AvalancheRecovery = 0.16f;
    public const float IcyRange = 8, IcyWidth = 2.5f, BlizzardRange = 10, BlizzardWidth = 4;
    public const float IcyCollisionWidth = 2; // S2B1; IcyWidth is the databin indicator width.
    public const float WindSpeed = 15, BlastSeparation = 0.25f, WindWindup = 0.15f, WindRecovery = 0.16f;
    public const float ShoveDistance = 1.5f, ShoveDuration = 0.2f;
    public const float FrostRadius = 2.75f, FrostDuration = 4, FrostInterval = 0.5f; // S21B1 MRadius=2750
    public const int FrostTicks = 8;
    public const float VeilDash = 4, VeilDashDuration = 0.25f, VeilRadius = 4, VeilDuration = 5; // S22B1 MRadius=4000
    public const float VeilWindup = 0.08f, VeilRecovery = 0.12f;
    public const float UniteRadius = 8, UniteWindup = 0.65f, UniteRecovery = 0.35f, UniteBuffDuration = 6;
    public const int UniteEnergy = 100000;
    public const ushort VeilHasteId = 65010, VeilSpeedId = 65011, UniteSpeedId = 65012;
    public const ushort UniteCdrId = 65013, UniteShieldId = 65014, UniteImmuneId = 65015;

    public static DamageInfo Special(ulong actor, float ratio, short slider, short flat)
        => new DamageInfo(actor, ratio, slider, flat, DamageType.Special);
    public static DamageInfo BasicDamage(ulong actor, bool boosted)
    {
        var damage = boosted ? DamageInfo.Tuned("alolanninetales.basicdamage", Special(actor, 0.5746f, 14, 270)) : DamageInfo.Tuned("alolanninetales.basicdamage.2", new DamageInfo(actor, 1, 0, 0, DamageType.Physical));
        damage.proprieties = DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit;
        return damage;
    }
    public static DamageInfo PassiveDamage(ulong actor) => DamageInfo.Tuned("alolanninetales.passivedamage", Special(actor, 0.6f, 0, 100));
    public static DamageInfo PowderDamage(ulong actor) => DamageInfo.Tuned("alolanninetales.powderdamage", Special(actor, 0.78f, 13, 240));
    public static DamageInfo AvalancheDamage(ulong actor) => DamageInfo.Tuned("alolanninetales.avalanchedamage", Special(actor, 0.44f, 10, 229));
    public static DamageInfo GleamDamage(ulong actor, bool plus) => plus ? DamageInfo.Tuned("alolanninetales.gleamdamage", Special(actor, 0.67f, 11, 418)) : DamageInfo.Tuned("alolanninetales.gleamdamage.2", Special(actor, 0.66f, 11, 352));
    public static DamageInfo IcyDamage(ulong actor) => DamageInfo.Tuned("alolanninetales.icydamage", Special(actor, 0.61f, 7, 240));
    public static DamageInfo BlizzardDamage(ulong actor, bool plus, int phase)
        => phase == 2 ? (plus ? DamageInfo.Tuned("alolanninetales.blizzarddamage", Special(actor, 0.18f, 3, 115)) : DamageInfo.Tuned("alolanninetales.blizzarddamage.2", Special(actor, 0.17f, 2, 110)))
            : phase == 1 ? (plus ? DamageInfo.Tuned("alolanninetales.blizzarddamage.3", Special(actor, 0.82f, 12, 540)) : DamageInfo.Tuned("alolanninetales.blizzarddamage.4", Special(actor, 0.78f, 8, 500)))
            : plus ? DamageInfo.Tuned("alolanninetales.blizzarddamage.5", Special(actor, 0.41f, 6, 270)) : DamageInfo.Tuned("alolanninetales.blizzarddamage.6", Special(actor, 0.39f, 4, 250));
    public static DamageInfo UniteDamage(ulong actor, bool frozen)
    {
        var damage = frozen ? DamageInfo.Tuned("alolanninetales.unitedamage", Special(actor, 2.42f, 35, 1640)) : DamageInfo.Tuned("alolanninetales.unitedamage.2", Special(actor, 1.59f, 32, 1150));
        damage.proprieties = DamageProprieties.IsUniteMove; return damage;
    }
    public static int VeilHeal(float specialAttack, int level) => Mathf.FloorToInt(specialAttack * 0.1f + level * 2 + 40);
    public static float BasicTimingScale(float cooldown) => Mathf.Clamp(cooldown / BasicReferenceCooldown, 0.2f, 1);
    public static string Animation(string action, bool evolved = true) => "ani_" + action + "_bat_" + (evolved ? "0038" : "0037");
    public static bool Living(Pokemon target) => target != null && target.IsSpawned && target.CurrentHp > 0;
    public static bool CanHit(PlayerManager actor, Pokemon target) => actor != null && Living(target)
        && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam) && !target.HasStatusEffect(StatusType.Invincible)
        && !target.HasStatusEffect(StatusType.Untargetable);
    public static void Refresh(Pokemon target, StatChange change) { target.RemoveAllStatChangeWithIDRPC(change.ID); target.AddStatChange(change); }
    public static void Shove(Pokemon target, Vector3 direction, float distance)
    {
        if (distance <= 0 || target.HasStatusEffect(StatusType.Unstoppable) || target.HasStatusEffect(StatusType.HindranceResistance)) return;
        if (target.TryGetComponent<PlayerMovement>(out var movement)) movement.DisplaceRPC(direction, distance, ShoveDuration);
        else if (target.TryGetComponent<WildPokemonAI>(out var wild)) wild.TryDisplace(direction, distance, ShoveDuration);
        else target.ApplyKnockbackRPC(direction, distance / ShoveDuration);
    }
}

// This range is separate from existing licenses' leased and fixed effect IDs.
public static class NinetalesEffectIds
{
    private static readonly HashSet<ushort> used = new HashSet<ushort>();
    public static ushort Acquire()
    {
        for (ushort id = 65100; id < 65500; id++) if (used.Add(id)) return id;
        throw new InvalidOperationException("Ninetales effect IDs exhausted.");
    }
    public static void Release(ushort id) { used.Remove(id); }
}

public sealed class NinetalesSnowStacks
{
    public int Count { get; private set; }
    public float Expires { get; private set; }
    public bool Add(float now)
    {
        if (now >= Expires) Count = 0;
        Expires = now + NinetalesMoveRules.SnowDuration;
        if (++Count < NinetalesMoveRules.FreezeStacks) return false;
        Clear(); return true;
    }
    public void Clear() { Count = 0; Expires = 0; }
}
