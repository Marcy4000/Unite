using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameResults 
{
    public bool BlueTeamWon { get; set; }
    public int BlueTeamScore { get; set; }
    public int OrangeTeamScore { get; set; }

    public float TotalGameTime { get; set; }

    public List<ResultScoreInfo> BlueTeamScores { get; set; }
    public List<ResultScoreInfo> OrangeTeamScores { get; set; }

    public GameResults(bool blueTeamWon, int blueTeamScore, int orangeTeamScore)
    {
        BlueTeamWon = blueTeamWon;
        BlueTeamScore = blueTeamScore;
        OrangeTeamScore = orangeTeamScore;
        BlueTeamScores = new List<ResultScoreInfo>();
        OrangeTeamScores = new List<ResultScoreInfo>();
    }

    public GameResults()
    {
        BlueTeamWon = false;
        BlueTeamScore = 0;
        OrangeTeamScore = 0;
        BlueTeamScores = new List<ResultScoreInfo>();
        OrangeTeamScores = new List<ResultScoreInfo>();
    }
}

public struct ResultScoreInfo
{
    public int ScoredPoints;
    public string PlayerID;
    public float time;

    public ResultScoreInfo(int scoredPoints, string playerID, float time)
    {
        ScoredPoints = scoredPoints;
        PlayerID = playerID;
        this.time = time;
    }
}