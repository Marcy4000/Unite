using System;
using UnityEngine;

public enum CresseliaCast { AuroraBeam, Safeguard, Psybeam, Moonblast, LunarBlessing, Moonlight }
public enum CresseliaEffect
{
    Basic, Boosted, AuroraBeam, Psybeam, Moonblast, MoonblastImpact, Cleanse,
    LunarShield, ShieldBreak, ShieldHeal, Moonlight, HealPulse, UniteFlight,
    UniteWarning, UniteImpact, Passive, BeamHeal
}

public static class CresseliaMoveRules
{
    public const string EffectFolder = "Assets/Prefabs/Objects/Moves/Cresselia/Rework/";
    public const float BoostCooldown = 6.5f, RhythmReduction = 1f;
    public const float BeamSpeed = 30f, BasicSpeed = 18f;
    public const float BlessingDuration = 3.5f, MoonlightDuration = 5f;
    public const int MoonlightPulses = 5;
    public const float FlightDuration = 3f, TakeoffDuration = 0.2f, LandingDuration = 0.3f, UniteRadius = 5.5f;
    public const ushort EscortId = 4880, UniteInvincibleId = 4881;
    public static readonly StatusType[] Interrupts = { StatusType.Incapacitated, StatusType.Asleep, StatusType.Frozen, StatusType.Bound };
    private static int nextCast;
    public static int NextCast() => ++nextCast;
    public static string EffectPath(CresseliaEffect effect) => EffectFolder + "Cresselia" + effect + ".prefab";
    public static bool IsSupport(CresseliaCast move) => move == CresseliaCast.Safeguard || move == CresseliaCast.LunarBlessing;
    public static int Slot(CresseliaCast move) => IsSupport(move) || move == CresseliaCast.Moonlight ? 1 : 0;
    public static float Cooldown(CresseliaCast move)
    {
        switch (move)
        {
            case CresseliaCast.AuroraBeam: return 5.5f;
            case CresseliaCast.Safeguard: return 8.5f;
            case CresseliaCast.Psybeam: return 7f;
            case CresseliaCast.Moonblast: return 7.5f;
            case CresseliaCast.LunarBlessing: return 9.5f;
            default: return 9f;
        }
    }
    public static float Range(CresseliaCast move) => IsSupport(move) ? 8f : move == CresseliaCast.Moonblast ? 6.5f
        : move == CresseliaCast.AuroraBeam ? 5.5f : move == CresseliaCast.Moonlight ? 5f : 6f;
    public static float Windup(CresseliaCast move) => IsSupport(move) ? 0f : move == CresseliaCast.Moonlight ? 0.15f : 0.22f;
    public static float Recovery(CresseliaCast move) => IsSupport(move) ? 0.2f : 0.18f;
    public static bool Upgraded(CresseliaCast move, int level) => level >= (Slot(move) == 0 ? 10 : 12);
    public static float MoonlightRadius(bool upgraded) => upgraded ? 3.5f : 3f;
    public static float PsybeamWidth(bool upgraded) => upgraded ? 1.3f : 1f;
    public static float MoonblastRadius(bool upgraded) => upgraded ? 1.5f : 1.2f;
    public static float ShieldRatio(bool upgraded) => upgraded ? 0.17f : 0.15f;
    public static float ReduceBoost(float remaining) => Mathf.Max(0, remaining - RhythmReduction);
    public static bool IsCleansable(StatusEffect effect) => effect.IsTimed && (effect.IsNegativeStatus()
        || effect.Type == StatusType.Burned || effect.Type == StatusType.Poisoned);
    public static Vector3 Direction(Vector3 value, Vector3 fallback)
    {
        value.y = 0;
        if (!Finite(value) || value.sqrMagnitude < 0.001f) { value = fallback; value.y = 0; }
        return Finite(value) && value.sqrMagnitude > 0.001f ? value.normalized : Vector3.forward;
    }
    public static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
        && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
    public static DamageInfo Damage(CresseliaCast move, ulong actor)
    {
        if (move == CresseliaCast.Psybeam) return new DamageInfo(actor, 0.95f, 8, 480, DamageType.Special);
        if (move == CresseliaCast.Moonblast) return new DamageInfo(actor, 1.27f, 7, 475, DamageType.Special);
        return new DamageInfo(actor, 0.75f, 5, 370, DamageType.Special);
    }
    public static DamageInfo BasicDamage(ulong actor, bool boosted) => new DamageInfo(actor, boosted ? 0.5f : 0.44f,
        (short)(boosted ? 18 : 10), (short)(boosted ? 280 : 100), DamageType.Special, DamageProprieties.IsBasicAttack);
}
