using UnityEngine;

// The shield itself is the buff's lifetime; scoring never owns a separate timer.
public static class RayquazaBuff
{
    public const ushort ShieldId = 65000;
    public const ushort ShieldPriority = ushort.MaxValue;
    public const float DamageMultiplier = 1.4f;

    public static ushort EnergyReward(bool secured) => (ushort)(secured ? 30 : 15);

    public static ShieldInfo CreateShield(int maxHp)
    {
        return new ShieldInfo(Mathf.FloorToInt(maxHp * 0.3f) + 3000, ShieldId, ShieldPriority, 0, false);
    }

    public static bool ProtectsScoring(bool shieldRemaining, DamageType damageType)
    {
        return shieldRemaining && damageType != DamageType.True;
    }
}
