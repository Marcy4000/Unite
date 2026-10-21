using UnityEngine;

public enum VaporCast { Swift, HelpingHand, AquaRing, RainDance, Whirlpool, Tempest }
public enum VaporEffect { Basic, Boosted, Swift, HelpingHand, AquaRing, ShieldBreak, HealPulse, Rain,
    Whirlpool, Collapse, DiveWake, DiveWarning, DiveSplash, TempestWarning, TempestImpact, Hydration, BoostReady, Aim }

// Custom-license tuning. Levels are zero-based, like Pokemon.CurrentLevel.
public static class VaporMoveRules
{
    public const string EffectFolder = "Assets/Prefabs/Objects/Moves/Vaporeon/Rework/";
    public const float BasicRange = 6f, BasicSpeed = 20f, BoostExpiry = 4.5f;
    public const float RingDuration = 6f, RingTick = 0.7f, RainDuration = 10f, RainRadius = 3f;
    public const float WhirlpoolDuration = 3f, WhirlpoolRadius = 2.5f, WhirlpoolCollapse = 0.9f;
    public const float PullDistance = 1.25f, PullImmunity = 1.5f;
    public const float DiveDuration = 3f, DiveMinimum = 0.25f, DiveWarning = 0.25f, DiveRadius = 2f;
    public const float TempestWindup = 0.6f, TempestRadius = 6f;
    public const ushort HydrationId = 1340;
    public static readonly StatusType[] Interrupts = { StatusType.Incapacitated, StatusType.Asleep, StatusType.Frozen, StatusType.Bound };
    public static string EffectPath(VaporEffect effect) => EffectFolder + "Vapor" + effect + ".prefab";
    public static bool IsSupport(VaporCast cast) => cast == VaporCast.HelpingHand || cast == VaporCast.AquaRing;
    public static int Slot(VaporCast cast) => cast == VaporCast.Tempest ? 2 : cast == VaporCast.HelpingHand || cast == VaporCast.Whirlpool ? 1 : 0;
    public static AvailableMoves Move(VaporCast cast)
    {
        switch (cast)
        {
            case VaporCast.Swift: return AvailableMoves.VaporSwift;
            case VaporCast.HelpingHand: return AvailableMoves.VaporHelpingHand;
            case VaporCast.AquaRing: return AvailableMoves.VaporAquaRing;
            case VaporCast.RainDance: return AvailableMoves.VaporRainDance;
            case VaporCast.Whirlpool: return AvailableMoves.VaporWhirlpool;
            default: return AvailableMoves.VaporeonUnite;
        }
    }
    public static float Cooldown(VaporCast cast) => cast == VaporCast.Swift ? 7.5f : cast == VaporCast.HelpingHand ? 9f
        : cast == VaporCast.AquaRing ? 12f : cast == VaporCast.RainDance ? 12.5f : cast == VaporCast.Whirlpool ? 11f : 0;
    public static float Range(VaporCast cast) => IsSupport(cast) ? 8f : cast == VaporCast.RainDance ? 4f : cast == VaporCast.Whirlpool ? 7f : 6f;
    public static float Windup(VaporCast cast) => cast == VaporCast.RainDance ? 0.35f : cast == VaporCast.Swift || cast == VaporCast.Whirlpool ? 0.2f : 0;
    public static float Recovery(VaporCast cast) => cast == VaporCast.Tempest ? 0.75f : cast == VaporCast.RainDance ? 0.1f : 0.18f;
    public static bool CommitOnStart(VaporCast cast) => cast == VaporCast.RainDance || Windup(cast) == 0;
    public static bool Upgraded(VaporCast cast, int level) => level >= (Slot(cast) == 0 ? 9 : 11);
    public static float ShieldRatio(bool upgraded) => upgraded ? 0.25f : 0.20f;
    public static short WhirlpoolSlow(bool upgraded) => (short)(upgraded ? 35 : 25);
    public static short RainDefense(bool upgraded) => (short)(upgraded ? 65 : 55);
    public static float DiveCooldown(bool upgraded) => upgraded ? 9f : 10f;
    public static int Heal(Pokemon actor, float ratio, int slider, int flat) => Mathf.FloorToInt(actor.GetSpAttack() * ratio + actor.CurrentLevel * slider + flat);
    public static DamageInfo BasicDamage(ulong actor, bool boosted) => new DamageInfo(actor, boosted ? 0.36f : 0.25f,
        (short)(boosted ? 11 : 8), (short)(boosted ? 180 : 170), DamageType.Special, DamageProprieties.IsBasicAttack);
    // Six ticks total 0.6 SpA + 6/level + 198, NOT that amount on every tick.
    public static DamageInfo WhirlpoolTick(ulong actor) => new DamageInfo(actor, 0.10f, 1, 33, DamageType.Special);
    public static DamageInfo TempestDamage(ulong actor) => new DamageInfo(actor, 0.92f, 9, 640, DamageType.Special, DamageProprieties.IsUniteMove);
    public static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z)
        && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
    public static Vector3 Direction(Vector3 value, Vector3 fallback)
    {
        value.y = 0; fallback.y = 0;
        if (!Finite(value) || value.sqrMagnitude < 0.001f) value = fallback;
        return Finite(value) && value.sqrMagnitude > 0.001f ? value.normalized : Vector3.forward;
    }
    public static Vector3 ClampDestination(Vector3 origin, Vector3 requested, float range)
    {
        if (!Finite(requested)) return origin;
        Vector3 offset = requested - origin; offset.y = 0;
        return origin + Vector3.ClampMagnitude(offset, range);
    }
}
