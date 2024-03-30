using System.Collections.Generic;

public static class ScoringSystem
{
    public static int baseScoreTime = 100; // Base time in percent to score without any modifiers

    // Dictionary to store the score speed factors for different buffs
    private readonly static Dictionary<string, int> scoreSpeedFactors = new Dictionary<string, int>()
    {
        { "GoalGetter", 1 },
        { "Rayquaza", 3 }
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
    public static float CalculateScoreTime(int numAllies)
    {
        int totalScoreSpeedFactor = 0;

        // Calculate total score speed factor from buffs
        foreach (var factor in scoreSpeedFactors.Values)
        {
            totalScoreSpeedFactor += factor;
        }

        // Calculate the score speed factor with allies
        float scoreSpeedFactorWithAllies = 1f - allyScoreTimeReductions[numAllies];

        // Calculate the total score speed factor
        float totalScoreSpeed = (float)(baseScoreTime + totalScoreSpeedFactor) / baseScoreTime;

        // Calculate the score time
        float scoreTime = 100f / totalScoreSpeed;

        // Apply score time reduction with allies
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
        else return 0f; // Handle scores outside the defined range
    }
}
