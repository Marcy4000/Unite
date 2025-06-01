using System.Collections.Generic;
using UnityEngine;

public static class RespawnSystem
{
    public static float CalculateRespawnTime(int level, int killsSinceLastDeath, int pointsSinceLastDeath, float timeRemaining)
    {
        var currentMap = CharactersList.Instance.GetCurrentLobbyMap();
        if (currentMap == null)
            return 0f;

        int L = GetLevelDuration(level, currentMap);
        float K = Mathf.Clamp(
            killsSinceLastDeath / currentMap.killsDivisor,
            currentMap.killsClampMin,
            currentMap.killsClampMax
        );
        float P = Mathf.Clamp(
            pointsSinceLastDeath / currentMap.pointsDivisor,
            currentMap.pointsClampMin,
            currentMap.pointsClampMax
        );
        int T = GetTimeBasedDuration(timeRemaining, currentMap);

        int result = Mathf.RoundToInt(L + K + P + T);

        return Mathf.Clamp(result, currentMap.timeClampMin, currentMap.timeClampMax);
    }

    private static int GetLevelDuration(int level, MapInfo map)
    {
        // level parte da 0, respawnLevelDurations ha 15 elementi (0-14)
        if (map.respawnLevelDurations != null && level >= 0 && level < map.respawnLevelDurations.Length)
        {
            return map.respawnLevelDurations[level];
        }
        return 4;
    }

    private static int GetTimeBasedDuration(float timeRemaining, MapInfo map)
    {
        if (map.respawnTimeThresholds != null && map.respawnTimeThresholds.Length > 0)
        {
            foreach (var threshold in map.respawnTimeThresholds)
            {
                if (timeRemaining >= threshold.minTime && timeRemaining < threshold.maxTime)
                {
                    return threshold.value;
                }
            }
        }
        // Se nessuna soglia corrisponde, ritorna 0 come default
        return 0;
    }
}
