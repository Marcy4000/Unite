using Unity.Netcode;

public enum DamageType : byte { Physical, Special, True }

public struct DamageInfo : INetworkSerializable
{
    //public Pokemon attacker;
    public ulong attackerId;
    public float ratio;
    public short slider;
    public short baseDmg;
    public DamageType type;

    public DamageInfo(ulong attackerId, float ratio, short slider, short baseDmg, DamageType type)
    {
        this.attackerId = attackerId;
        this.ratio = ratio;
        this.slider = slider;
        this.baseDmg = baseDmg;
        this.type = type;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref attackerId);
        serializer.SerializeValue(ref ratio);
        serializer.SerializeValue(ref slider);
        serializer.SerializeValue(ref baseDmg);
        serializer.SerializeValue(ref type);
    }
}
