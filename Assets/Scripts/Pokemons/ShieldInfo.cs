using System;
using Unity.Netcode;

public struct ShieldInfo : INetworkSerializable, IEquatable<ShieldInfo>
{
    public int Amount;
    public ushort ID;
    public ushort Priority;

    public float Duration;
    public bool IsTimed;
    public ushort MaxStacks;

    public ShieldInfo(int amount, ushort id, ushort priority, float duration, bool isTimed, ushort maxStacks = 0)
    {
        Amount = amount;
        ID = id;
        Priority = priority;
        Duration = duration;
        IsTimed = isTimed;
        MaxStacks = maxStacks;
    }

    public bool Equals(ShieldInfo other)
    {
        return Amount == other.Amount && ID == other.ID && Priority == other.Priority && Duration == other.Duration && IsTimed == other.IsTimed && MaxStacks == other.MaxStacks;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Amount);
        serializer.SerializeValue(ref ID);
        serializer.SerializeValue(ref Priority);
        serializer.SerializeValue(ref Duration);
        serializer.SerializeValue(ref IsTimed);
        serializer.SerializeValue(ref MaxStacks);
    }
}
