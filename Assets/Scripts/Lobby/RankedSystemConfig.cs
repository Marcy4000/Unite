using UnityEngine;

[CreateAssetMenu(fileName = "RankedSystemConfig", menuName = "Ranked System/Config")]
public class RankedSystemConfig : ScriptableObject
{
    private static RankedSystemConfig _instance;
    public static RankedSystemConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<RankedSystemConfig>("RankedSystemConfig");
                if (_instance == null)
                {
                    Debug.LogError("RankedSystemConfig not found in Resources folder! Creating default config.");
                    _instance = CreateDefaultConfig();
                }
            }
            return _instance;
        }
    }

    [SerializeField] private RankInfo[] ranks = new RankInfo[6];

    private void OnEnable()
    {
        if (ranks == null || ranks.Length != 6)
        {
            InitializeDefaultRanks();
        }
    }

    private void InitializeDefaultRanks()
    {
        ranks = new RankInfo[6];
        ranks[0] = new RankInfo("Beginner", 3, 2, false); // Bronze
        ranks[1] = new RankInfo("Great", 4, 3, false); // Silver
        ranks[2] = new RankInfo("Expert", 5, 4, false); // Gold
        ranks[3] = new RankInfo("Veteran", 5, 5, false); // Platinum
        ranks[4] = new RankInfo("Ultra", 5, 6, false); // Diamond
        ranks[5] = new RankInfo("Master", 1, 0, true); // Master (special)
    }

    private static RankedSystemConfig CreateDefaultConfig()
    {
        var config = CreateInstance<RankedSystemConfig>();
        config.InitializeDefaultRanks();
        return config;
    }

    public RankInfo GetRankConfig(int rankIndex)
    {
        if (rankIndex < 0 || rankIndex >= ranks.Length)
        {
            Debug.LogError($"Invalid rank index: {rankIndex}");
            return ranks[0]; // Return Beginner as fallback
        }
        return ranks[rankIndex];
    }

    public int GetTotalRanks() => ranks.Length;

    public bool IsValidRankIndex(int index) => index >= 0 && index < ranks.Length;

    public string GetRankDisplayName(int rankIndex, int classIndex)
    {
        var rank = GetRankConfig(rankIndex);
        if (rank.isSpecialRank)
        {
            return rank.rankName;
        }
        return $"{rank.rankName} Class {classIndex}";
    }

    // Editor utility to reset to defaults
    [ContextMenu("Reset to Default Configuration")]
    public void ResetToDefaults()
    {
        InitializeDefaultRanks();
    }
}
