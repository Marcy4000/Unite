using System.Collections.Generic;
using UnityEngine;

public static class RespawnSystem
{
    public static float CalculateRespawnTime(int level, int killsSinceLastDeath, int pointsSinceLastDeath, float timeRemaining)
    {
        int L = GetLevelDuration(level);
        float K = Mathf.Clamp(killsSinceLastDeath / 2f, 0f, 10f);
        float P = Mathf.Clamp(pointsSinceLastDeath / 60f, 0f, 10f);
        int T = GetTimeBasedDuration(timeRemaining);

        int result = Mathf.RoundToInt(L + K + P + T);

        return Mathf.Clamp(result, 0, 45);
    }

    private static int GetLevelDuration(int level)
    {
        Dictionary<int, int> levelDurations = new Dictionary<int, int>
        {
            { 1, 4 }, { 2, 4 }, { 3, 4 }, { 4, 4 },
            { 5, 5 }, { 6, 6 }, { 7, 8 }, { 8, 9 },
            { 9, 10 }, { 10, 11 }, { 11, 12 }, { 12, 15 },
            { 13, 17 }, { 14, 19 }, { 15, 19 }
        };

        if (levelDurations.TryGetValue(level+1, out int duration))
        {
            return duration;
        }
        return 4;
    }

    private static int GetTimeBasedDuration(float timeRemaining)
    {
        if (timeRemaining > 120)
        {
            return 0;
        }
        else if (timeRemaining > 60)
        {
            return 4;
        }
        else
        {
            return 10;
        }
    }
}
