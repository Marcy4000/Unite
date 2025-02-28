using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum ScoreSpeedFactor
{
    GoalGetter,
    Rayquaza
}

public struct ScoreBoost : INetworkSerializable, IEquatable<ScoreBoost>
{
    public ushort ID;
    public ScoreSpeedFactor ScoreSpeedFactor;
    public float Duration;
    public bool IsTimed;

    public ScoreBoost(ushort id, ScoreSpeedFactor scoreSpeedFactor, float duration, bool isTimed)
    {
        ID = id;
        ScoreSpeedFactor = scoreSpeedFactor;
        Duration = duration;
        IsTimed = isTimed;
    }

    public bool Equals(ScoreBoost other)
    {
        if (ID != other.ID || ScoreSpeedFactor != other.ScoreSpeedFactor || Duration != other.Duration || IsTimed != other.IsTimed)
        {
            return false;
        }

        return true;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ID);
        serializer.SerializeValue(ref ScoreSpeedFactor);
        serializer.SerializeValue(ref Duration);
        serializer.SerializeValue(ref IsTimed);
    }
}

public class ScoringSystem
{
    public static int baseScoreTime = 100; // Base time in percent to score without any modifiers

    // Dictionary to store the score speed factors for different buffs
    private readonly static Dictionary<ScoreSpeedFactor, int> scoreSpeedFactors = new Dictionary<ScoreSpeedFactor, int>()
    {
        { ScoreSpeedFactor.GoalGetter, 1 },
        { ScoreSpeedFactor.Rayquaza, 3 }
        // Add more buffs as needed
    };

    // Dictionary to store the score time reductions for different numbers of allies
    private readonly static Dictionary<int, float> allyScoreTimeReductions = new Dictionary<int, float>()
    {
        { 0, 0f },
        { 1, 0.30f },
        { 2, 0.35f },
        { 3, 0.40f },
        { 4, 0.60f }
    };

    // Calculate the score time based on modifiers
    public static float CalculateScoreTime(int numAllies, List<ScoreBoost> Buffs)
    {
        int totalScoreSpeedFactor = 1; // Start with the base factor 1

        foreach (var buff in Buffs)
        {
            if (scoreSpeedFactors.ContainsKey(buff.ScoreSpeedFactor))
            {
                totalScoreSpeedFactor += scoreSpeedFactors[buff.ScoreSpeedFactor];
            }
        }

        // Calculate the score time as the inverse of the score speed factor
        float scoreTime = Mathf.Pow(totalScoreSpeedFactor, -1);

        // Apply score time reduction with allies
        float scoreSpeedFactorWithAllies = 1f - allyScoreTimeReductions[numAllies];
        scoreTime *= scoreSpeedFactorWithAllies;

        return scoreTime;
    }

    // Calculate approximate times to score without any modifiers
    public static float CalculateApproximateScoreTime(int score)
    {
        if (score >= 1 && score <= 6) return 0.5f;
        else if (score >= 7 && score <= 13) return 1.1f;
        else if (score >= 14 && score <= 24) return 2f;
        else if (score >= 25 && score <= 33) return 3f;
        else if (score >= 34 && score <= 40) return 4f;
        else if (score >= 41 && score <= 45) return 4.5f;
        else if (score >= 46 && score <= 50) return 5.3f;
        else return 0f;
    }

    // Calculate the true score time
    public static float CalculateTrueScoreTime(int numAllies, List<ScoreBoost> Buffs, int score)
    {
        // Calculate the score time based on buffs and allies
        float scoreTimeWithBuffsAndAllies = CalculateScoreTime(numAllies, Buffs);

        // Calculate the approximate score time based on the score value
        float approximateScoreTime = CalculateApproximateScoreTime(score);

        // Combine both factors
        float trueScoreTime = scoreTimeWithBuffsAndAllies * approximateScoreTime;

        return trueScoreTime;
    }
}