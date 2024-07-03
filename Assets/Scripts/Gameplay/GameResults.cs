using Unity.Netcode;

public struct GameResults : INetworkSerializable
{
    public bool BlueTeamWon;
    public ushort BlueTeamScore;
    public ushort OrangeTeamScore;

    public float TotalGameTime;

    public ResultScoreInfo[] BlueTeamScores;
    public ResultScoreInfo[] OrangeTeamScores;

    public PlayerStats[] PlayerStats;

    public GameResults(bool blueTeamWon, ushort blueTeamScore, ushort orangeTeamScore, float totalGameTime, ResultScoreInfo[] blueTeamScores, ResultScoreInfo[] orangeTeamScores, PlayerStats[] playerStats)
    {
        BlueTeamWon = blueTeamWon;
        BlueTeamScore = blueTeamScore;
        OrangeTeamScore = orangeTeamScore;
        TotalGameTime = totalGameTime;
        BlueTeamScores = blueTeamScores;
        OrangeTeamScores = orangeTeamScores;
        PlayerStats = playerStats;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref BlueTeamWon);
        serializer.SerializeValue(ref BlueTeamScore);
        serializer.SerializeValue(ref OrangeTeamScore);
        serializer.SerializeValue(ref TotalGameTime);

        serializer.SerializeValue(ref BlueTeamScores);
        serializer.SerializeValue(ref OrangeTeamScores);
        serializer.SerializeValue(ref PlayerStats);
    }
}

public struct ResultScoreInfo : INetworkSerializable
{
    public ushort ScoredPoints;
    public string PlayerID;
    public float Time;

    public ResultScoreInfo(ushort scoredPoints, string playerID, float time)
    {
        ScoredPoints = scoredPoints;
        PlayerID = playerID;
        Time = time;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ScoredPoints);
        serializer.SerializeValue(ref PlayerID);
        serializer.SerializeValue(ref Time);
    }
}