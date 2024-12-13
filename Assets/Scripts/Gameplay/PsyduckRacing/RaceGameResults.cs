using Unity.Netcode;

public struct RaceGameResults : INetworkSerializable
{
    public RacePlayerResult[] PlayerResults;
    public float TotalRaceTime;

    public RaceGameResults(RacePlayerResult[] playerResults, float totalRaceTime)
    {
        PlayerResults = playerResults;
        TotalRaceTime = totalRaceTime;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerResults);
        serializer.SerializeValue(ref TotalRaceTime);
    }
}

public struct RacePlayerResult : INetworkSerializable
{
    public string PlayerID;
    public short Position;
    public float FinishTime;

    public RacePlayerResult(string playerID, short position, float finishTime)
    {
        PlayerID = playerID;
        Position = position;
        FinishTime = finishTime;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerID);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref FinishTime);
    }
}
