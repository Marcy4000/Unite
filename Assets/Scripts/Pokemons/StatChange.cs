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
    CritModifier
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
    }

    public bool Equals(StatChange other)
    {
        if (Amount != other.Amount || AffectedStat != other.AffectedStat || Duration != other.Duration || IsTimed != other.IsTimed || IsBuff != other.IsBuff || ID != other.ID || Percentage != other.Percentage || CanBeRemoved != other.CanBeRemoved)
        {
            return false;
        }

        return true;
    }

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
    }
}
