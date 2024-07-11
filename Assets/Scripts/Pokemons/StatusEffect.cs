using System;
using Unity.Netcode;

public enum StatusType : byte { Immobilized, Incapacitated, Asleep, Frozen, Bound, Unstoppable, Invincible, Untargetable, HindranceResistance, Invisible, VisionObscuring, Scriptable }

public struct StatusEffect : INetworkSerializable, IEquatable<StatusEffect>
{
    public StatusType Type;
    public float Duration;
    public bool IsTimed;

    public ushort ID;

    public StatusEffect(StatusType type, float duration, bool isTimed, ushort id)
    {
        Type = type;
        Duration = duration;
        IsTimed = isTimed;
        ID = id;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Type);
        serializer.SerializeValue(ref Duration);
        serializer.SerializeValue(ref IsTimed);
        serializer.SerializeValue(ref ID);
    }

    public bool IsNegativeStatus()
    {
        return Type == StatusType.Immobilized || Type == StatusType.Incapacitated || Type == StatusType.Asleep || Type == StatusType.Frozen || Type == StatusType.Bound || Type == StatusType.VisionObscuring;
    }

    public bool Equals(StatusEffect other)
    {
        if (Type != other.Type || Duration != other.Duration || IsTimed != other.IsTimed || ID != other.ID)
        {
            return false;
        }

        return true;
    }
}
