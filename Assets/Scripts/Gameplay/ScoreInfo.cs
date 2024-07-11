using Unity.Netcode;

public struct ScoreInfo : INetworkSerializable
{
    public ushort scoredPoints;
    public ulong scorerId;

    public ScoreInfo(ushort scoredPoints, ulong scorerId)
    {
        this.scoredPoints = scoredPoints;
        this.scorerId = scorerId;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref scoredPoints);
        serializer.SerializeValue(ref scorerId);
    }
}
