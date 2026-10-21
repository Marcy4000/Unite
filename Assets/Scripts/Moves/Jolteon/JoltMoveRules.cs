using UnityEngine;

public enum JoltEffect { Agility, Charge, ThunderFang, Discharge, Electroweb, WebMark, LightningBlitz, Overcharge, Ready }

public static class JoltMoveRules
{
    public const string EffectFolder = "Assets/Prefabs/Objects/Moves/Jolteon/Rework/";
    public static string EffectPath(JoltEffect effect) => EffectFolder + effect + ".prefab";
    public const float FangStartup = 0.18f, FangDuration = 0.2f, FangRange = 4f, FangWindow = 4f;
    public const float DischargeRange = 8f, BoostedDischargeRange = 10f, DischargeRadius = 2.75f;
    public const float RecastScale = 0.7f, DischargeWindow = 3f, DischargeTick = 0.45f;
    public const float WebRange = 9f, WebSpeed = 12f, WebWidth = 3.5f, BoostedWebWidth = 5f;
    public const float WebDuration = 4f, UpgradedWebDuration = 5f, WebTick = 0.6f;
    public const short ChargeSlow = 75;
    public const float ChargeSlowDuration = 0.3f, BlitzDuration = 3.5f, BlitzSpeed = 25f;
    public const ushort ChargeBuff = 1351, ChargeSlowId = 1352, FangSpeed = 1353, WebSpeedId = 1354;
    public const ushort DischargeSlow = 1355, BlitzSlow = 1356, BlitzUnstoppable = 1357, OverchargeSpeed = 1358;
    // Duraludon reserves 20000..39999 for Pulse/Tail marks.
    public const ushort WebMarkIdMin = 40000, WebMarkIdMax = 59999;
    // Like Duraludon's secondary damage, this flag prevents periodic damage from
    // triggering on-move-hit rewards. Basic attacks and Swift are not changed.
    public const DamageProprieties PeriodicDamage = DamageProprieties.Unused_2;
    public const DamageProprieties DirectMoveDamage = DamageProprieties.Unused_1;
    public static bool TriggersWebSpeed(DamageProprieties flags)
    {
        return (flags & (DirectMoveDamage | DamageProprieties.IsUniteMove)) != 0
            && (flags & (DamageProprieties.IsBasicAttack | DamageProprieties.IsMuscleBand | DamageProprieties.IsHeal | PeriodicDamage)) == 0;
    }
    private static int nextCast;
    public static int NextCast() => ++nextCast;

    public static float EffectDuration(JoltEffect effect, bool empowered, bool upgraded)
    {
        switch (effect)
        {
            case JoltEffect.Agility: return 3f;
            case JoltEffect.Charge: return empowered ? 6f : 5f;
            case JoltEffect.ThunderFang: return FangStartup + FangDuration;
            case JoltEffect.Discharge: return 2.5f;
            case JoltEffect.Electroweb: return WebRange / WebSpeed;
            case JoltEffect.WebMark: return upgraded ? UpgradedWebDuration : WebDuration;
            case JoltEffect.LightningBlitz: return BlitzDuration;
            case JoltEffect.Overcharge: return 5f;
            case JoltEffect.Ready: return 65f;
            default: return 1f;
        }
    }

    public static Vector3 Reflect(Vector3 direction, Vector3 normal)
    {
        Vector3 reflected = Vector3.Reflect(direction.normalized, normal);
        reflected.y = 0;
        return reflected.sqrMagnitude > 0.001f ? reflected.normalized : -direction.normalized;
    }
}

// Testable resource state. A death reset must bypass overcharge's spend protection.
public sealed class JoltChargeState
{
    public float Amount { get; private set; }
    public float OverchargeUntil { get; private set; }
    public bool Ready => Amount >= 100f;
    public void Add(float amount) { Amount = Mathf.Clamp(Amount + amount, 0f, 100f); }
    public bool Consume(float now)
    {
        if (!Ready) return false;
        if (now >= OverchargeUntil) Amount = 0f;
        return true;
    }
    public void Reduce(float amount, float now) { if (now >= OverchargeUntil) Add(-amount); }
    public void Overcharge(float now, float duration) { Amount = 100f; OverchargeUntil = now + duration; }
    public void Clear() { Amount = 0f; OverchargeUntil = 0f; }
}

// A temporary earned follow-up, NOT independently rechargeable stored uses.
public sealed class JoltRecastState
{
    public bool Empowered { get; private set; }
    public bool IsSecondPass { get; private set; }
    public bool HasWindow { get; private set; }
    public float Until { get; private set; }
    public void Begin(bool empowered) { Clear(); Empowered = empowered; }
    public void Open(float now, float duration)
    {
        if (IsSecondPass) return;
        HasWindow = true;
        Until = now + duration;
    }
    public bool TryRecast(float now)
    {
        if (!HasWindow || now >= Until) return false;
        HasWindow = false;
        IsSecondPass = true;
        return true;
    }
    public bool Expired(float now) => HasWindow && now >= Until;
    public void Clear() { Empowered = false; IsSecondPass = false; HasWindow = false; Until = 0f; }
}
