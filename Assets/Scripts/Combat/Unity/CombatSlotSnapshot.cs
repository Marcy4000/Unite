using System;
using Unity.Netcode;

public struct CombatSlotSnapshot : INetworkSerializable, IEquatable<CombatSlotSnapshot>
{
    public int Revision;
    public AvailableMoves MoveId;
    public int Charges;
    public double CooldownUntil;
    public bool Reserved;
    public bool AwaitingEnd;
    public bool FollowUpReady;
    public long CastId;
    public ulong FollowUpTargetId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Revision);
        serializer.SerializeValue(ref MoveId);
        serializer.SerializeValue(ref Charges);
        serializer.SerializeValue(ref CooldownUntil);
        serializer.SerializeValue(ref Reserved);
        serializer.SerializeValue(ref AwaitingEnd);
        serializer.SerializeValue(ref FollowUpReady);
        serializer.SerializeValue(ref CastId);
        serializer.SerializeValue(ref FollowUpTargetId);
    }

    public bool Equals(CombatSlotSnapshot other) => Revision == other.Revision && MoveId == other.MoveId && Charges == other.Charges
        && CooldownUntil.Equals(other.CooldownUntil) && Reserved == other.Reserved && AwaitingEnd == other.AwaitingEnd
        && FollowUpReady == other.FollowUpReady && CastId == other.CastId && FollowUpTargetId == other.FollowUpTargetId;
    public override bool Equals(object other) => other is CombatSlotSnapshot snapshot && Equals(snapshot);
    public override int GetHashCode() => HashCode.Combine(HashCode.Combine(Revision, MoveId, Charges, CooldownUntil, Reserved, AwaitingEnd, FollowUpReady, CastId), FollowUpTargetId);
}
