using UnityEngine;
using System;

public class RankedManager : MonoBehaviour
{
    public static RankedManager Instance { get; private set; }

    private PlayerRankData playerRankData;
    private const string RANK_DATA_KEY = "PlayerRankData";

    public event Action<PlayerRankData, PlayerRankData> OnRankChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        LoadRankData();
    }

    private void LoadRankData()
    {
        string savedData = PlayerPrefs.GetString(RANK_DATA_KEY, "");
        if (string.IsNullOrEmpty(savedData))
        {
            playerRankData = PlayerRankData.GetDefault();
            SaveRankData();
        }
        else
        {
            playerRankData = PlayerRankData.Deserialize(savedData);
        }
    }

    private void SaveRankData()
    {
        PlayerPrefs.SetString(RANK_DATA_KEY, playerRankData.Serialize());
        PlayerPrefs.Save();
    }

    public string GetPlayerRankName()
    {
        return RankedSystemConfig.Instance.GetRankConfig(playerRankData.currentRankIndex).rankName;
    }

    public int GetPlayerClass()
    {
        return playerRankData.currentClass;
    }

    public int GetPlayerDiamonds()
    {
        return playerRankData.currentDiamonds;
    }

    public int GetPlayerWins()
    {
        return playerRankData.totalWins;
    }

    public int GetPlayerLosses()
    {
        return playerRankData.totalLosses;
    }

    public string GetPlayerRankSerialized()
    {
        return playerRankData.Serialize();
    }

    public PlayerRankData GetPlayerRankData()
    {
        return playerRankData;
    }

    public string GetRankDisplayString()
    {
        var rankConfig = RankedSystemConfig.Instance.GetRankConfig(playerRankData.currentRankIndex);
        if (rankConfig.isSpecialRank)
        {
            return $"{rankConfig.rankName} ({playerRankData.currentDiamonds} Diamonds)";
        }
        return $"{rankConfig.rankName} Class {playerRankData.currentClass} ({playerRankData.currentDiamonds} Diamonds)";
    }

    public PlayerRankData GetPlayerRankFromLobby(string playerId)
    {
        var player = LobbyController.Instance.GetPlayerByID(playerId);
        if (player?.Data.ContainsKey("PlayerRank") == true)
        {
            return PlayerRankData.Deserialize(player.Data["PlayerRank"].Value);
        }
        return PlayerRankData.GetDefault();
    }

    public string GetPlayerRankDisplayFromLobby(string playerId)
    {
        var rankData = GetPlayerRankFromLobby(playerId);
        var rankConfig = RankedSystemConfig.Instance.GetRankConfig(rankData.currentRankIndex);
        
        if (rankConfig.isSpecialRank)
        {
            return $"{rankConfig.rankName} ({rankData.currentDiamonds})";
        }
        return $"{rankConfig.rankName} Class {rankData.currentClass}";
    }

    public void ProcessMatchResult(bool won, bool wasMatchmaking)
    {
        if (!wasMatchmaking)
        {
            Debug.Log("Rank unchanged: Not a matchmaking game");
            return;
        }

        var oldRankData = playerRankData;

        if (won)
        {
            GainDiamond();
        }
        else
        {
            LoseDiamond();
        }

        SaveRankData();
        UpdateLobbyRankData();

        if (oldRankData.currentRankIndex != playerRankData.currentRankIndex || 
            oldRankData.currentClass != playerRankData.currentClass)
        {
            OnRankChanged?.Invoke(oldRankData, playerRankData);
        }

        LogRankChange(won, oldRankData, playerRankData);
    }

    private void GainDiamond()
    {
        playerRankData.totalWins++;
        var currentRankConfig = RankedSystemConfig.Instance.GetRankConfig(playerRankData.currentRankIndex);

        if (currentRankConfig.isSpecialRank)
        {
            playerRankData.currentDiamonds++;
            Debug.Log($"Master rank: Gained diamond. Total: {playerRankData.currentDiamonds}");
            return;
        }

        playerRankData.currentDiamonds++;

        if (playerRankData.currentDiamonds > currentRankConfig.diamondsToPromote)
        {
            PromotePlayer();
        }
    }

    private void LoseDiamond()
    {
        playerRankData.totalLosses++;
        var currentRankConfig = RankedSystemConfig.Instance.GetRankConfig(playerRankData.currentRankIndex);

        if (currentRankConfig.isSpecialRank)
        {
            if (playerRankData.currentDiamonds > 0)
            {
                playerRankData.currentDiamonds--;
                Debug.Log($"Master rank: Lost diamond. Total: {playerRankData.currentDiamonds}");
                return;
            }
            else
            {
                DemotePlayer();
                Debug.Log("Master rank: Lost at 0 diamonds, demoted to previous rank.");
                return;
            }
        }

        if (playerRankData.currentDiamonds == 0)
        {
            DemotePlayer();
        }
        else
        {
            playerRankData.currentDiamonds--;
        }
    }

    private void PromotePlayer()
    {
        var currentRankConfig = RankedSystemConfig.Instance.GetRankConfig(playerRankData.currentRankIndex);

        if (playerRankData.currentClass > 1)
        {
            playerRankData.currentClass--;
            playerRankData.currentDiamonds = 1;
            Debug.Log($"Promoted to {GetRankDisplayString()}");
        }
        else
        {
            if (playerRankData.currentRankIndex < RankedSystemConfig.Instance.GetTotalRanks() - 1)
            {
                playerRankData.currentRankIndex++;
                var newRankConfig = RankedSystemConfig.Instance.GetRankConfig(playerRankData.currentRankIndex);
                playerRankData.currentClass = (byte)newRankConfig.totalClasses;
                playerRankData.currentDiamonds = 1;
                Debug.Log($"Promoted to new rank: {GetRankDisplayString()}");
            }
            else
            {
                Debug.LogWarning("Tried to promote beyond highest rank");
            }
        }
    }

    private void DemotePlayer()
    {
        if (playerRankData.currentClass < RankedSystemConfig.Instance.GetRankConfig(playerRankData.currentRankIndex).totalClasses)
        {
            playerRankData.currentClass++;
            var currentRankConfig = RankedSystemConfig.Instance.GetRankConfig(playerRankData.currentRankIndex);
            playerRankData.currentDiamonds = (ushort)currentRankConfig.diamondsToPromote;
            Debug.Log($"Demoted to {GetRankDisplayString()}");
        }
        else
        {
            if (playerRankData.currentRankIndex > 0)
            {
                playerRankData.currentRankIndex--;
                var newRankConfig = RankedSystemConfig.Instance.GetRankConfig(playerRankData.currentRankIndex);
                playerRankData.currentClass = 1;
                playerRankData.currentDiamonds = (ushort)(newRankConfig.diamondsToPromote - 1);
                Debug.Log($"Demoted to previous rank: {GetRankDisplayString()}");
            }
            else
            {
                playerRankData.currentDiamonds = 0;
                Debug.Log("At lowest possible rank, diamonds set to 0");
            }
        }
    }

    private void UpdateLobbyRankData()
    {
        if (LobbyController.Instance != null)
        {
            LobbyController.Instance.UpdatePlayerRank();
        }
    }

    private void LogRankChange(bool won, PlayerRankData oldRank, PlayerRankData newRank)
    {
        string result = won ? "Won" : "Lost";
        string oldRankStr = RankedSystemConfig.Instance.GetRankDisplayName(oldRank.currentRankIndex, oldRank.currentClass);
        string newRankStr = RankedSystemConfig.Instance.GetRankDisplayName(newRank.currentRankIndex, newRank.currentClass);
        
        Debug.Log($"Ranked Match {result}: {oldRankStr} ({oldRank.currentDiamonds} diamonds) → {newRankStr} ({newRank.currentDiamonds} diamonds)");
    }

    [ContextMenu("Reset Rank Data")]
    public void ResetRankData()
    {
        var oldRank = playerRankData;
        playerRankData = PlayerRankData.GetDefault();
        SaveRankData();
        UpdateLobbyRankData();
        OnRankChanged?.Invoke(oldRank, playerRankData);
        Debug.Log("Rank data reset to default");
    }

    [ContextMenu("Add Test Win")]
    public void AddTestWin()
    {
        ProcessMatchResult(true, true);
    }

    [ContextMenu("Add Test Loss")]
    public void AddTestLoss()
    {
        ProcessMatchResult(false, true);
    }

    [ContextMenu("Set Rank to Ultra1-5Diamonds")]
    public void SetRankToRank4Class1FiveDiamonds()
    {
        var oldRank = playerRankData;
        playerRankData.currentRankIndex = 4;
        playerRankData.currentClass = 1;
        playerRankData.currentDiamonds = 5;
        SaveRankData();
        UpdateLobbyRankData();
        OnRankChanged?.Invoke(oldRank, playerRankData);
        Debug.Log("Rank set to Rank 4, Class 1, 5 Diamonds");
    }
}
