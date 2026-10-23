using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>Source-owned periodic damage carried by a status, not by a character or its victim's input controller.</summary>
[Serializable]
public struct PeriodicDamageInfo : INetworkSerializable, IEquatable<PeriodicDamageInfo>
{
    public DamageInfo Damage;
    public float Interval;
    public float TargetMaxHpRatio;
    public short NonPlayerDamageCap;
    public bool Enabled => Interval != 0;
    public bool IsValid => float.IsFinite(Interval) && Interval >= 0.05f && float.IsFinite(TargetMaxHpRatio)
        && TargetMaxHpRatio >= 0 && TargetMaxHpRatio <= 1 && float.IsFinite(Damage.ratio) && Damage.ratio >= 0
        && float.IsFinite(Damage.multiplier) && Damage.multiplier >= 0 && NonPlayerDamageCap >= 0;

    public PeriodicDamageInfo(DamageInfo damage, float interval, float targetMaxHpRatio = 0, short nonPlayerDamageCap = 0)
    {
        Damage = damage;
        // Periodic damage must never masquerade as an attack, crit, Unite or item proc.
        Damage.proprieties = DamageProprieties.Unused_2;
        Interval = interval; TargetMaxHpRatio = targetMaxHpRatio; NonPlayerDamageCap = nonPlayerDamageCap;
    }

    public DamageInfo TickDamage(int maxHp, int attack, int level, bool nonPlayer)
    {
        DamageInfo result = Damage;
        int flat = Mathf.Max(0, result.baseDmg) + Mathf.FloorToInt(Mathf.Max(0, maxHp) * TargetMaxHpRatio);
        if (nonPlayer && NonPlayerDamageCap > 0)
        {
            // Cap the complete pre-mitigation tick, including its source-stat scaling.
            float multiplier = result.multiplier > 0 ? result.multiplier : 1;
            flat = Mathf.Min(NonPlayerDamageCap, Mathf.FloorToInt((flat + result.ratio * attack + result.slider * level) * multiplier));
            result.ratio = 0; result.slider = 0; result.multiplier = 1;
        }
        result.baseDmg = (short)Mathf.Clamp(flat, 0, short.MaxValue);
        result.proprieties = DamageProprieties.Unused_2;
        return result;
    }

    public bool Equals(PeriodicDamageInfo other) => Damage.Equals(other.Damage) && Interval == other.Interval
        && TargetMaxHpRatio == other.TargetMaxHpRatio && NonPlayerDamageCap == other.NonPlayerDamageCap;
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Damage); serializer.SerializeValue(ref Interval);
        serializer.SerializeValue(ref TargetMaxHpRatio); serializer.SerializeValue(ref NonPlayerDamageCap);
    }
}

/// <summary>Absolute schedule: refresh never postpones a due tick and the final boundary tick is not lost.</summary>
public sealed class PeriodicDamageClock
{
    public double NextTick { get; private set; }
    public double Expires { get; private set; }
    public double Interval { get; private set; }
    public PeriodicDamageClock(double now, double duration, double interval)
    {
        Interval = interval; NextTick = now + interval; Expires = now + duration;
    }
    public void Refresh(double now, double duration, double interval)
    {
        Expires = now + duration; Interval = interval;
        NextTick = Math.Min(NextTick, now + interval);
    }
    public void Extend(double now, double minimumRemaining) { Expires = Math.Max(Expires, now + minimumRemaining); }
    public void SetRemaining(double now, double remaining) { Expires = now + remaining; }
    public bool TryTick(double now)
    {
        if (now + 0.00001 < NextTick || NextTick > Expires + 0.00001) return false;
        NextTick += Interval;
        return true;
    }
}
