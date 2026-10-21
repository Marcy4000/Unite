using UnityEngine;

public enum MarshadowEffect { Punch, BoostedPunch, DrainPunch, ShadowSneak, AuraSphere, AuraMark, Rush, SpectralThief, Theft, PhantomForce, Kick, WallImpact, MirrorShadow, Passive, EchoBurst, EchoField }
public enum MarshadowHit { DrainPunch, Rush, SpectralThief, Kick, AuraFollowup }

public static class MarshadowMoveRules
{
    public const string EffectFolder = "Assets/Prefabs/Objects/Moves/Marshadow/Rework/";
    public const float PassiveDuration = 4f;
    public const float AuraRange = 5.5f;
    public const float AuraWindow = 5f;
    public const float AuraSpeed = 24f;
    public const float RushRange = 3.5f;
    public const float RushDuration = 0.23f;
    public const float RushWindow = 1.25f;
    public const float SpectralRange = 4f;
    public const float SpectralRadius = 1.5f;
    public const float TheftRadius = 0.8f;
    public const ushort AttackBuff = 62000, AttackSpeedBuff = 62001, LifeStealBuff = 62002;
    public const ushort SneakSpeed = 62003, SneakInvincible = 62004, SneakInvisible = 62005;
    public const ushort PhantomInvincible = 62006, PhantomInvisible = 62007;
    public const ushort Slow = 62008, FinisherSpeed = 62009, EchoBuff = 62010;
    private static int nextCast;
    public static int NextCast() { return ++nextCast; }
    public static string EffectPath(MarshadowEffect effect) => EffectFolder + effect + ".prefab";
    public static Vector3 Direction(Vector3 value, Vector3 fallback)
    {
        value.y = 0;
        if (value.sqrMagnitude < 0.001f) { value = fallback; value.y = 0; }
        return value.sqrMagnitude > 0.001f ? value.normalized : Vector3.forward;
    }
    public static int AuraExecute(int missingHp, bool upgraded, bool wild)
    {
        int amount = 250 + Mathf.RoundToInt(Mathf.Max(0, missingHp) * (upgraded ? 0.25f : 0.2f));
        return wild ? Mathf.Min(700, amount) : Mathf.Min(short.MaxValue, amount);
    }
    public static bool CanSteal(StatChange buff, float remaining) => buff.IsBuff && buff.IsTimed && buff.CanBeRemoved && remaining > 0;
    public static float StolenDuration(float remaining, bool upgraded) => Mathf.Clamp(remaining, 0, upgraded ? 8f : 6f);
}

// Deterministic state shared by the passive and tests; no timers hidden in buffs.
public sealed class MarshadowStackState
{
    public int Count { get; private set; }
    public float ExpiresAt { get; private set; }
    public void Gain(float now) { Tick(now); Count = Mathf.Min(3, Count + 1); ExpiresAt = now + MarshadowMoveRules.PassiveDuration; }
    public bool Tick(float now)
    {
        if (Count == 0 || now < ExpiresAt) return false;
        Clear(); return true;
    }
    public void Clear() { Count = 0; ExpiresAt = 0; }
}
