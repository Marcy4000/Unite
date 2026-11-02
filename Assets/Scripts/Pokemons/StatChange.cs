using System;
using Unity.Netcode;

public enum Stat : byte
{
    Attack,
    Defense,
    SpAttack,
    SpDefense,
    CritRate,
    Cdr,
    LifeSteal,
    AtkSpeed,
    Speed,
    DamageReduction,
    MaxHp,
    DefensePenetration,
    CritModifier,
    SpellVamp,
    PhysicalDamageReduction
}

[Serializable]
public struct StatChange : INetworkSerializable, IEquatable<StatChange>
{
    public short Amount;
    public Stat AffectedStat;
    public float Duration;
    public bool IsTimed;
    public bool IsBuff;
    public bool Percentage;
    public bool CanBeRemoved;

    public ushort ID;
    public ulong SourceId;
    public bool HasSource;

    public StatChange(short amount, Stat affectedStat, float duration, bool isTimed, bool isBuff, bool percentage, ushort id, bool canBeRemoved=true)
    {
        Amount = amount;
        AffectedStat = affectedStat;
        Duration = duration;
        IsTimed = isTimed;
        IsBuff = isBuff;
        ID = id;
        Percentage = percentage;
        CanBeRemoved = canBeRemoved;
        SourceId = 0; HasSource = false;
    }

    public StatChange WithSource(ulong source) { SourceId = source; HasSource = true; return this; }

    public bool Equals(StatChange other)
    {
        // Attribution is metadata, not identity: legacy callers remove effects
        // using their original value, before server-side sender attribution.
        if (Amount != other.Amount || AffectedStat != other.AffectedStat || Duration != other.Duration || IsTimed != other.IsTimed || IsBuff != other.IsBuff || ID != other.ID || Percentage != other.Percentage || CanBeRemoved != other.CanBeRemoved)
        {
            return false;
        }

        return true;
    }

    public override bool Equals(object other) => other is StatChange change && Equals(change);
    public override int GetHashCode() => HashCode.Combine(Amount, AffectedStat, Duration, IsTimed, IsBuff, Percentage, CanBeRemoved, ID);

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Amount);
        serializer.SerializeValue(ref AffectedStat);
        serializer.SerializeValue(ref Duration);
        serializer.SerializeValue(ref IsTimed);
        serializer.SerializeValue(ref IsBuff);
        serializer.SerializeValue(ref Percentage);
        serializer.SerializeValue(ref ID);
        serializer.SerializeValue(ref CanBeRemoved);
        serializer.SerializeValue(ref SourceId);
        serializer.SerializeValue(ref HasSource);
    }
}
