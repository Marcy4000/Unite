using System;
using Unity.Netcode;

public enum StatusType : byte { Immobilized, Incapacitated, Asleep, Frozen, Bound, Unstoppable, Invincible, Untargetable, HindranceResistance, Invisible, VisionObscuring, Scriptable, Burned, Poisoned, RedBuff, BlueBuff, SnowMarked, FutureSightMarked }

public struct StatusEffect : INetworkSerializable, IEquatable<StatusEffect>
{
    public StatusType Type;
    public float Duration;
    public bool IsTimed;

    public ushort ID;

    public PeriodicDamageInfo PeriodicDamage;
    public ulong SourceId;
    public bool HasSource;

    public StatusEffect(StatusType type, float duration, bool isTimed, ushort id)
    {
        Type = type;
        Duration = duration;
        IsTimed = isTimed;
        ID = id;
        PeriodicDamage = default;
        SourceId = 0; HasSource = false;
    }

    public StatusEffect WithSource(ulong source) { SourceId = source; HasSource = true; return this; }

    public static StatusEffect DamageOverTime(StatusType type, DamageInfo damage, float duration, float interval,
        ushort id = 0, float targetMaxHpRatio = 0, short nonPlayerDamageCap = 0)
    {
        return new StatusEffect(type, duration, true, id)
        { PeriodicDamage = new PeriodicDamageInfo(damage, interval, targetMaxHpRatio, nonPlayerDamageCap), SourceId = damage.attackerId, HasSource = true };
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
        serializer.SerializeValue(ref SourceId);
        serializer.SerializeValue(ref HasSource);
    }

    public bool IsNegativeStatus()
    {
        return Type == StatusType.Immobilized || Type == StatusType.Incapacitated || Type == StatusType.Asleep || Type == StatusType.Frozen || Type == StatusType.Bound || Type == StatusType.VisionObscuring || Type == StatusType.SnowMarked || Type == StatusType.FutureSightMarked;
    }

    public bool Equals(StatusEffect other)
    {
        // Preserve legacy value matching after server-side sender attribution.
        if (Type != other.Type || Duration != other.Duration || IsTimed != other.IsTimed || ID != other.ID || !PeriodicDamage.Equals(other.PeriodicDamage))
        {
            return false;
        }

        return true;
    }

    public override bool Equals(object other) => other is StatusEffect effect && Equals(effect);
    public override int GetHashCode() => HashCode.Combine(Type, Duration, IsTimed, ID, PeriodicDamage);
}
