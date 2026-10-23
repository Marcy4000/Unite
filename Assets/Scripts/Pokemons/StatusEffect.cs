using System;
using Unity.Netcode;

public enum StatusType : byte { Immobilized, Incapacitated, Asleep, Frozen, Bound, Unstoppable, Invincible, Untargetable, HindranceResistance, Invisible, VisionObscuring, Scriptable, Burned, Poisoned, RedBuff, BlueBuff }

public struct StatusEffect : INetworkSerializable, IEquatable<StatusEffect>
{
    public StatusType Type;
    public float Duration;
    public bool IsTimed;

    public ushort ID;

    public PeriodicDamageInfo PeriodicDamage;

    public StatusEffect(StatusType type, float duration, bool isTimed, ushort id)
    {
        Type = type;
        Duration = duration;
        IsTimed = isTimed;
        ID = id;
        PeriodicDamage = default;
    }

    public static StatusEffect DamageOverTime(StatusType type, DamageInfo damage, float duration, float interval,
        ushort id = 0, float targetMaxHpRatio = 0, short nonPlayerDamageCap = 0)
    {
        return new StatusEffect(type, duration, true, id)
        { PeriodicDamage = new PeriodicDamageInfo(damage, interval, targetMaxHpRatio, nonPlayerDamageCap) };
    }

    public bool SamePeriodicSource(StatusEffect other) => PeriodicDamage.Enabled && other.PeriodicDamage.Enabled
        && Type == other.Type && ID == other.ID && PeriodicDamage.Damage.attackerId == other.PeriodicDamage.Damage.attackerId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Type);
        serializer.SerializeValue(ref Duration);
        serializer.SerializeValue(ref IsTimed);
        serializer.SerializeValue(ref ID);
        serializer.SerializeValue(ref PeriodicDamage);
    }

    public bool IsNegativeStatus()
    {
        return Type == StatusType.Immobilized || Type == StatusType.Incapacitated || Type == StatusType.Asleep || Type == StatusType.Frozen || Type == StatusType.Bound || Type == StatusType.VisionObscuring;
    }

    public bool Equals(StatusEffect other)
    {
        if (Type != other.Type || Duration != other.Duration || IsTimed != other.IsTimed || ID != other.ID || !PeriodicDamage.Equals(other.PeriodicDamage))
        {
            return false;
        }

        return true;
    }
}
