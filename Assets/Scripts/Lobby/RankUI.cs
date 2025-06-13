using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RankUI : MonoBehaviour
{
    [Header("Basic UI Elements")]
    [SerializeField] private TMP_Text rankName;
    [SerializeField] private GameObject diamondsHolder;
    [SerializeField] private Image rankTrophyIcon;
    
    [Header("Diamond Prefabs")]
    [SerializeField] private GameObject fullDiamondPrefab;
    [SerializeField] private GameObject emptyDiamondPrefab;
    
    [Header("Master Rank Special UI")]
    [SerializeField] private GameObject normalRankUI;
    [SerializeField] private GameObject masterRankUI;
    [SerializeField] private TMP_Text masterDiamondCount;
    
    private List<GameObject> spawnedDiamonds = new List<GameObject>();

    private void Start()
    {
        if (LobbyController.Instance != null)
        {
            LobbyController.Instance.onLobbyUpdate += OnLobbyUpdate;
        }
        
        UpdateRankDisplay();
    }

    private void OnDestroy()
    {
        if (LobbyController.Instance != null)
        {
            LobbyController.Instance.onLobbyUpdate -= OnLobbyUpdate;
        }
    }

    private void OnLobbyUpdate(Unity.Services.Lobbies.Models.Lobby lobby)
    {
        UpdateRankDisplay();
    }

    public void UpdateRankDisplay()
    {
        if (RankedManager.Instance == null)
        {
            SetErrorState();
            return;
        }

        var rankData = RankedManager.Instance.GetPlayerRankData();
        var rankConfig = RankedSystemConfig.Instance.GetRankConfig(rankData.currentRankIndex);

        UpdateRankText(rankData, rankConfig);
        UpdateTrophyIcon(rankConfig);
        UpdateDiamondDisplay(rankData, rankConfig);
    }

    private void UpdateRankText(PlayerRankData rankData, RankInfo rankConfig)
    {
        if (rankName == null) return;

        if (rankConfig.isSpecialRank)
        {
            rankName.text = rankConfig.rankName;
        }
        else
        {
            rankName.text = $"{rankConfig.rankName} {rankData.currentClass}";
        }
    }

    private void UpdateTrophyIcon(RankInfo rankConfig)
    {
        if (rankTrophyIcon == null) return;

        if (rankConfig.rankIcon != null)
        {
            rankTrophyIcon.sprite = rankConfig.rankIcon;
            rankTrophyIcon.color = Color.white;
        }
        else
        {
            rankTrophyIcon.sprite = null;
        }
    }

    private void UpdateDiamondDisplay(PlayerRankData rankData, RankInfo rankConfig)
    {
        ClearDiamonds();

        if (rankConfig.isSpecialRank)
        {
            ShowMasterRankUI(rankData.currentDiamonds);
        }
        else
        {
            ShowNormalRankUI(rankData.currentDiamonds, rankConfig.diamondsToPromote);
        }
    }

    private void ShowMasterRankUI(int currentDiamonds)
    {
        if (normalRankUI != null) normalRankUI.SetActive(false);
        if (masterRankUI != null) masterRankUI.SetActive(true);

        if (masterDiamondCount != null)
        {
            masterDiamondCount.text = $"x {currentDiamonds}";
        }
    }

    private void ShowNormalRankUI(int currentDiamonds, int maxDiamonds)
    {
        if (normalRankUI != null) normalRankUI.SetActive(true);
        if (masterRankUI != null) masterRankUI.SetActive(false);

        if (diamondsHolder == null || fullDiamondPrefab == null || emptyDiamondPrefab == null)
        {
            Debug.LogWarning("RankUI: Missing required prefabs or holder for diamond display");
            return;
        }

        for (int i = 0; i < currentDiamonds && i < maxDiamonds; i++)
        {
            GameObject diamond = Instantiate(fullDiamondPrefab, diamondsHolder.transform);
            spawnedDiamonds.Add(diamond);
        }

        for (int i = currentDiamonds; i < maxDiamonds; i++)
        {
            GameObject diamond = Instantiate(emptyDiamondPrefab, diamondsHolder.transform);
            spawnedDiamonds.Add(diamond);
        }
    }

    private void ClearDiamonds()
    {
        foreach (var diamond in spawnedDiamonds)
        {
            if (diamond != null)
            {
                DestroyImmediate(diamond);
            }
        }
        spawnedDiamonds.Clear();
    }

    private void SetErrorState()
    {
        if (rankName != null)
        {
            rankName.text = "Rank System Not Available";
            rankName.color = Color.gray;
        }

        if (rankTrophyIcon != null)
        {
            rankTrophyIcon.sprite = null;
            rankTrophyIcon.color = Color.gray;
        }

        ClearDiamonds();

        if (normalRankUI != null) normalRankUI.SetActive(true);
        if (masterRankUI != null) masterRankUI.SetActive(false);
    }

    public void RefreshDisplay()
    {
        UpdateRankDisplay();
    }

    [ContextMenu("Force Update Display")]
    private void ForceUpdateDisplay()
    {
        UpdateRankDisplay();
    }
}
