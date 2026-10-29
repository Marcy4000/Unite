using Unity.Netcode;

public struct GameResults : INetworkSerializable
{
    public Team WinningTeam;
    public bool Surrendered;
    public ushort BlueTeamScore;
    public ushort OrangeTeamScore;

    public float TotalGameTime;

    public ResultScoreInfo[] BlueTeamScores;
    public ResultScoreInfo[] OrangeTeamScores;

    public PlayerStats[] PlayerStats;

    public GameResults(Team winningTeam, bool surrendered, ushort blueTeamScore, ushort orangeTeamScore, float totalGameTime, ResultScoreInfo[] blueTeamScores, ResultScoreInfo[] orangeTeamScores, PlayerStats[] playerStats)
    {
        WinningTeam = winningTeam;
        Surrendered = surrendered;
        BlueTeamScore = blueTeamScore;
        OrangeTeamScore = orangeTeamScore;
        TotalGameTime = totalGameTime;
        BlueTeamScores = blueTeamScores;
        OrangeTeamScores = orangeTeamScores;
        PlayerStats = playerStats;
    }

    public int GetAnimatedScore(Team team, float elapsedTime, bool completed)
    {
        int total = team == Team.Orange ? OrangeTeamScore : BlueTeamScore;
        if (completed)
        {
            return total;
        }

        ResultScoreInfo[] scores = team == Team.Orange ? OrangeTeamScores : BlueTeamScores;
        int animatedScore = 0;
        if (scores != null)
        {
            foreach (ResultScoreInfo score in scores)
            {
                if (score.Time <= elapsedTime)
                {
                    animatedScore += score.ScoredPoints;
                }
            }
        }

        return System.Math.Min(animatedScore, total);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref WinningTeam);
        serializer.SerializeValue(ref Surrendered);
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
