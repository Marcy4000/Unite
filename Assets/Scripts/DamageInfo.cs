using System;
using Unity.Netcode;

public enum DamageType : byte { Physical, Special, True }

[Flags]
public enum DamageProprieties : byte
{
    None = 0,
    CanCrit = 1 << 0,
    IsBasicAttack = 1 << 1,
    IsUniteMove = 1 << 2,
    IsHeal = 1 << 3,
    IsMuscleBand = 1 << 4,
    WasCriticalHit = 1 << 5,
    Unused_2 = 1 << 6,
    Unused_1 = 1 << 7
}

public struct DamageInfo : INetworkSerializable
{
    public ulong attackerId;
    public float ratio;
    public short slider;
    public short baseDmg;
    public DamageType type;
    public DamageProprieties proprieties;

    public DamageInfo(ulong attackerId, float ratio, short slider, short baseDmg, DamageType type, DamageProprieties proprieties = DamageProprieties.None)
    {
        this.attackerId = attackerId;
        this.ratio = ratio;
        this.slider = slider;
        this.baseDmg = baseDmg;
        this.type = type;
        this.proprieties = proprieties;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref attackerId);
        serializer.SerializeValue(ref ratio);
        serializer.SerializeValue(ref slider);
        serializer.SerializeValue(ref baseDmg);
        serializer.SerializeValue(ref type);
        serializer.SerializeValue(ref proprieties);
    }
}
