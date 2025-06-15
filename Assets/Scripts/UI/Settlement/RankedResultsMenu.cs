using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using JSAM;

public class RankedResultsMenu : MonoBehaviour
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
    
    [Header("Navigation")]
    [SerializeField] private Button continueButton;
    [SerializeField] private GameObject rankedResultsUI;
    
    [Header("Animation Settings")]
    [SerializeField] private float trophyPopDuration = 1.2f;
    [SerializeField] private float diamondAnimDuration = 0.8f;
    [SerializeField] private float trophySwitchDelay = 1.0f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugMode = false;
    
    private PlayerRankData previousRankData;
    private PlayerRankData currentRankData;
    private List<GameObject> spawnedDiamonds = new List<GameObject>();
    
    private SettlementManager settlementManager;
    
    private bool isAnimating = false;
    private Sequence currentSequence;

    private void Awake()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }
        
        if (rankedResultsUI != null)
        {
            rankedResultsUI.SetActive(false);
        }
    }

    public void Initialize(SettlementManager manager, PlayerRankData previousRank, PlayerRankData currentRank)
    {
        settlementManager = manager;
        previousRankData = previousRank;
        currentRankData = currentRank;
        
        Debug.Log($"RankedResultsMenu: Previous rank - {previousRank.currentRankIndex} Class {previousRank.currentClass} ({previousRank.currentDiamonds} diamonds)");
        Debug.Log($"RankedResultsMenu: Current rank - {currentRank.currentRankIndex} Class {currentRank.currentClass} ({currentRank.currentDiamonds} diamonds)");
    }

    public void ShowMenu()
    {
        if (rankedResultsUI != null)
        {
            rankedResultsUI.SetActive(true);
        }
        
        StartRankAnimation();
    }

    public void HideMenu()
    {
        if (rankedResultsUI != null)
        {
            rankedResultsUI.SetActive(false);
        }
        
        if (currentSequence != null)
        {
            currentSequence.Kill();
            currentSequence = null;
        }
    }

    private void StartRankAnimation()
    {
        if (RankedSystemConfig.Instance == null)
        {
            SetErrorState();
            return;
        }

        if (currentSequence != null)
        {
            currentSequence.Kill(true);
            currentSequence = null;
        }
        
        if (rankTrophyIcon != null)
        {
            rankTrophyIcon.transform.DOKill(true);
            rankTrophyIcon.DOKill(true);
        }
        
        if (continueButton != null)
        {
            continueButton.transform.DOKill(true);
        }

        if (isAnimating)
        {
            Debug.LogWarning("Forcing animation restart");
        }

        isAnimating = true;
        
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(false);
        }

        SetupInitialState();

        if (IsPromotion())
        {
            StartPromotionAnimation();
        }
        else if (IsDemotion())
        {
            StartDemotionAnimation();
        }
        else
        {
            StartDiamondChangeAnimation();
        }
    }

    private void SetupInitialState()
    {
        if (rankTrophyIcon != null)
        {
            rankTrophyIcon.transform.localScale = Vector3.zero;
            rankTrophyIcon.color = new Color(1f, 1f, 1f, 0f);
        }

        var previousRankConfig = RankedSystemConfig.Instance.GetRankConfig(previousRankData.currentRankIndex);
        
        UpdateRankText(previousRankData, previousRankConfig);
        
        if (rankTrophyIcon != null && previousRankConfig.rankIcon != null)
        {
            rankTrophyIcon.sprite = previousRankConfig.rankIcon;
        }
        
        UpdateDiamondDisplay(previousRankData, previousRankConfig);
    }

    private void StartPromotionAnimation()
    {
        float extraDelay = Mathf.Max(0, trophySwitchDelay - trophyPopDuration);
        
        var currentRankConfig = RankedSystemConfig.Instance.GetRankConfig(currentRankData.currentRankIndex);
        var previousRankConfig = RankedSystemConfig.Instance.GetRankConfig(previousRankData.currentRankIndex);
        
        currentSequence = DOTween.Sequence()
            .Append(AnimateTrophyPopIn())
            .AppendCallback(() => {
                AudioManager.PlaySound(DefaultAudioSounds.Play_PaiWei_Levelup);
            })
            .AppendInterval(extraDelay)
            .Append(AnimateTrophyUpgrade());
            
        if (currentRankConfig.isSpecialRank && !previousRankConfig.isSpecialRank)
        {
            currentSequence.Append(AnimatePromotionToMaster());
        }
        else if (!currentRankConfig.isSpecialRank)
        {
            currentSequence.Append(AnimatePromotionDiamonds());
        }
        
        currentSequence.AppendCallback(OnAnimationComplete);
    }

    private void StartDemotionAnimation()
    {
        float extraDelay = Mathf.Max(0, trophySwitchDelay - trophyPopDuration);
        
        currentSequence = DOTween.Sequence()
            .Append(AnimateTrophyPopIn())
            .AppendCallback(() => {
                AudioManager.PlaySound(DefaultAudioSounds.Play_PaiWei_LevelDown);
            })
            .AppendInterval(extraDelay)
            .Append(AnimateTrophyUpgrade())
            .Append(AnimateDemotionDiamonds())
            .AppendCallback(OnAnimationComplete);
    }

    private void StartDiamondChangeAnimation()
    {
        bool isGainingDiamond = currentRankData.currentDiamonds > previousRankData.currentDiamonds;
        
        var currentRankConfig = RankedSystemConfig.Instance.GetRankConfig(currentRankData.currentRankIndex);
        var previousRankConfig = RankedSystemConfig.Instance.GetRankConfig(previousRankData.currentRankIndex);
        
        currentSequence = DOTween.Sequence()
            .Append(AnimateTrophyPopIn())
            .AppendCallback(() => {
                AudioManager.PlaySound(isGainingDiamond ? DefaultAudioSounds.Play_PaiWei_Star_Up : DefaultAudioSounds.Play_PaiWei_Star_Down);
            });
            
        if (currentRankConfig.isSpecialRank && previousRankConfig.isSpecialRank)
        {
            currentSequence.Append(AnimateMasterDiamondChange(isGainingDiamond));
        }
        else
        {
            currentSequence.Append(AnimateDiamondChange(isGainingDiamond));
        }
        
        currentSequence.AppendCallback(OnAnimationComplete);
    }

    private Tween AnimateTrophyPopIn()
    {
        AudioManager.PlaySound(DefaultAudioSounds.Play_PaiWei_JiangBei);
        
        if (rankTrophyIcon != null)
        {
            rankTrophyIcon.transform.localScale = Vector3.zero;
            rankTrophyIcon.color = new Color(1f, 1f, 1f, 0f);
        }
        
        return DOTween.Sequence()
            .Append(rankTrophyIcon.transform.DOScale(1.2f, trophyPopDuration * 0.7f).SetEase(Ease.OutBack))
            .Join(rankTrophyIcon.DOFade(1f, trophyPopDuration * 0.5f).SetEase(Ease.OutQuad))
            .Append(rankTrophyIcon.transform.DOScale(1f, trophyPopDuration * 0.3f).SetEase(Ease.InQuad));
    }

    private Tween AnimateTrophyUpgrade()
    {
        var currentRankConfig = RankedSystemConfig.Instance.GetRankConfig(currentRankData.currentRankIndex);
        float upgradeDuration = 1.0f;
        
        return DOTween.Sequence()
            .Append(rankTrophyIcon.transform.DOScale(0.8f, upgradeDuration * 0.3f))
            .Join(rankTrophyIcon.DOFade(0f, upgradeDuration * 0.3f))
            .AppendCallback(() => {
                if (currentRankConfig.rankIcon != null)
                {
                    rankTrophyIcon.sprite = currentRankConfig.rankIcon;
                }
                UpdateRankText(currentRankData, currentRankConfig);
            })
            .Append(rankTrophyIcon.DOFade(1f, upgradeDuration * 0.4f))
            .Join(rankTrophyIcon.transform.DOScale(1.1f, upgradeDuration * 0.4f))
            .Append(rankTrophyIcon.transform.DOScale(1f, upgradeDuration * 0.3f));
    }

    private Tween AnimatePromotionDiamonds()
    {
        var currentRankConfig = RankedSystemConfig.Instance.GetRankConfig(currentRankData.currentRankIndex);
        
        return DOTween.Sequence()
            .AppendCallback(() => {
                ClearDiamonds();
                ShowNormalRankUI(0, currentRankConfig.diamondsToPromote);
            })
            .AppendInterval(0.1f)
            .AppendCallback(() => {
                if (spawnedDiamonds.Count > 0)
                {
                    var emptyDiamond = spawnedDiamonds[0];
                    var fullDiamond = Instantiate(fullDiamondPrefab, diamondsHolder.transform);
                    
                    fullDiamond.transform.SetSiblingIndex(emptyDiamond.transform.GetSiblingIndex());
                    
                    fullDiamond.transform.position = emptyDiamond.transform.position;
                    fullDiamond.transform.localScale = Vector3.zero;
                    
                    emptyDiamond.SetActive(false);
                    
                    fullDiamond.transform.DOScale(1f, diamondAnimDuration).SetEase(Ease.OutBack)
                        .OnComplete(() => {
                            if (emptyDiamond != null)
                            {
                                DestroyImmediate(emptyDiamond);
                            }
                            spawnedDiamonds[0] = fullDiamond;
                        });
                }
            });
    }

    private Tween AnimateDemotionDiamonds()
    {
        var currentRankConfig = RankedSystemConfig.Instance.GetRankConfig(currentRankData.currentRankIndex);
        
        return DOTween.Sequence()
            .AppendCallback(() => {
                ClearDiamonds();
                ShowNormalRankUI(currentRankConfig.diamondsToPromote, currentRankConfig.diamondsToPromote);
            })
            .AppendInterval(0.1f)
            .AppendCallback(() => {
                if (spawnedDiamonds.Count > 0)
                {
                    var lastDiamond = spawnedDiamonds[spawnedDiamonds.Count - 1];
                    var emptyDiamond = Instantiate(emptyDiamondPrefab, diamondsHolder.transform);
                    
                    emptyDiamond.transform.SetSiblingIndex(lastDiamond.transform.GetSiblingIndex());
                    
                    emptyDiamond.transform.position = lastDiamond.transform.position;
                    emptyDiamond.transform.localScale = Vector3.zero;
                    
                    lastDiamond.SetActive(false);
                    
                    emptyDiamond.transform.DOScale(1f, diamondAnimDuration).SetEase(Ease.OutBack)
                        .OnComplete(() => {
                            if (lastDiamond != null)
                            {
                                DestroyImmediate(lastDiamond);
                            }
                            spawnedDiamonds[spawnedDiamonds.Count - 1] = emptyDiamond;
                        });
                }
            });
    }

    private Tween AnimatePromotionToMaster()
    {
        return DOTween.Sequence()
            .AppendCallback(() => {
                ClearDiamonds();
                ShowMasterRankUI(currentRankData.currentDiamonds);
                
                if (masterDiamondCount != null)
                {
                    masterDiamondCount.transform.localScale = Vector3.zero;
                    masterDiamondCount.transform.DOScale(1f, diamondAnimDuration).SetEase(Ease.OutBack);
                }
            })
            .AppendInterval(diamondAnimDuration);
    }

    private Tween AnimateMasterDiamondChange(bool isAdding)
    {
        int diamondDifference = (int)currentRankData.currentDiamonds - (int)previousRankData.currentDiamonds;
        string changeText = isAdding ? $"(+{diamondDifference})" : $"({diamondDifference})";
        
        return DOTween.Sequence()
            .AppendCallback(() => {
                if (masterDiamondCount != null)
                {
                    masterDiamondCount.text = $"x {currentRankData.currentDiamonds} {changeText}";
                    masterDiamondCount.transform.localScale = Vector3.one;
                    
                    masterDiamondCount.transform.DOScale(1.2f, diamondAnimDuration * 0.3f).SetEase(Ease.OutQuad)
                        .OnComplete(() => {
                            masterDiamondCount.transform.DOScale(1f, diamondAnimDuration * 0.2f).SetEase(Ease.InQuad);
                        });
                }
            })
            .AppendInterval(diamondAnimDuration * 0.6f)
            .AppendCallback(() => {
                if (masterDiamondCount != null)
                {
                    masterDiamondCount.text = $"x {currentRankData.currentDiamonds}";
                }
            })
            .AppendInterval(diamondAnimDuration * 0.4f);
    }

    private Tween AnimateDiamondChange(bool isAdding)
    {
        return DOTween.Sequence()
            .AppendCallback(() => {
                if (isAdding)
                {
                    AnimateAddDiamond();
                }
                else
                {
                    AnimateRemoveDiamond();
                }
            })
            .AppendInterval(diamondAnimDuration + 0.1f);
    }

    private void AnimateAddDiamond()
    {
        for (int i = 0; i < spawnedDiamonds.Count; i++)
        {
            var diamond = spawnedDiamonds[i];
            if (IsEmptyDiamond(diamond))
            {
                var fullDiamond = Instantiate(fullDiamondPrefab, diamondsHolder.transform);
                
                fullDiamond.transform.SetSiblingIndex(diamond.transform.GetSiblingIndex());
                
                fullDiamond.transform.position = diamond.transform.position;
                fullDiamond.transform.localScale = Vector3.zero;
                
                diamond.SetActive(false);
                
                fullDiamond.transform.DOScale(1f, diamondAnimDuration).SetEase(Ease.OutBack)
                    .OnComplete(() => {
                        if (diamond != null)
                        {
                            DestroyImmediate(diamond);
                        }
                        spawnedDiamonds[i] = fullDiamond;
                    });
                break;
            }
        }
    }

    private void AnimateRemoveDiamond()
    {
        for (int i = spawnedDiamonds.Count - 1; i >= 0; i--)
        {
            var diamond = spawnedDiamonds[i];
            if (IsFullDiamond(diamond))
            {
                var emptyDiamond = Instantiate(emptyDiamondPrefab, diamondsHolder.transform);
                
                emptyDiamond.transform.SetSiblingIndex(diamond.transform.GetSiblingIndex());
                
                emptyDiamond.transform.position = diamond.transform.position;
                emptyDiamond.transform.localScale = Vector3.zero;
                
                diamond.transform.DOScale(0f, diamondAnimDuration * 0.5f).SetEase(Ease.InBack)
                    .OnComplete(() => {
                        diamond.SetActive(false);
                    });
                
                emptyDiamond.transform.DOScale(1f, diamondAnimDuration).SetEase(Ease.OutBack).SetDelay(diamondAnimDuration * 0.3f)
                    .OnComplete(() => {
                        if (diamond != null)
                        {
                            DestroyImmediate(diamond);
                        }
                        spawnedDiamonds[i] = emptyDiamond;
                    });
                break;
            }
        }
    }

    private bool IsEmptyDiamond(GameObject diamond)
    {
        if (diamond == null) return false;
        return diamond.name.Contains("empty") || 
               (emptyDiamondPrefab != null && diamond.GetComponent<Image>()?.sprite == emptyDiamondPrefab.GetComponent<Image>()?.sprite);
    }

    private bool IsFullDiamond(GameObject diamond)
    {
        if (diamond == null) return false;
        return diamond.name.Contains("full") || 
               (fullDiamondPrefab != null && diamond.GetComponent<Image>()?.sprite == fullDiamondPrefab.GetComponent<Image>()?.sprite);
    }

    private void OnAnimationComplete()
    {
        isAnimating = false;
        
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
            continueButton.transform.localScale = Vector3.zero;
            continueButton.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
        }
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
            rankName.text = $"{rankConfig.rankName} Class {rankData.currentClass}";
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
            Debug.LogWarning("RankedResultsMenu: Missing required prefabs or holder for diamond display");
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

    private void OnContinueClicked()
    {
        HideMenu();
        
        if (settlementManager != null)
        {
            settlementManager.GoToTeamMenu();
        }
    }

    public PlayerRankData GetPreviousRankData() => previousRankData;
    public PlayerRankData GetCurrentRankData() => currentRankData;
    
    public bool HasRankChanged()
    {
        return previousRankData.currentRankIndex != currentRankData.currentRankIndex || 
               previousRankData.currentClass != currentRankData.currentClass;
    }
    
    public bool IsPromotion()
    {
        if (previousRankData.currentRankIndex < currentRankData.currentRankIndex)
            return true;
        
        if (previousRankData.currentRankIndex == currentRankData.currentRankIndex && 
            previousRankData.currentClass > currentRankData.currentClass)
            return true;
            
        return false;
    }
    
    public bool IsDemotion()
    {
        if (previousRankData.currentRankIndex > currentRankData.currentRankIndex)
            return true;
        
        if (previousRankData.currentRankIndex == currentRankData.currentRankIndex && 
            previousRankData.currentClass < currentRankData.currentClass)
            return true;
            
        return false;
    }

    private void OnDestroy()
    {
        ClearDiamonds();
        
        if (currentSequence != null)
        {
            currentSequence.Kill();
            currentSequence = null;
        }
    }

    [ContextMenu("Replay Rank Animation")]
    private void DebugReplayAnimation()
    {
        if (!enableDebugMode)
        {
            Debug.LogWarning("Debug mode is disabled");
            return;
        }
        
        if (previousRankData.Equals(default(PlayerRankData)) || currentRankData.Equals(default(PlayerRankData)))
        {
            Debug.LogWarning("No rank data available for replay. Run Initialize first.");
            return;
        }
        
        Debug.Log("Replaying rank animation...");
        
        if (currentSequence != null)
        {
            currentSequence.Kill();
            currentSequence = null;
        }
        
        isAnimating = false;
        StartRankAnimation();
    }

    [ContextMenu("Test Promotion Animation")]
    private void DebugPromotionAnimation()
    {
        if (!enableDebugMode)
        {
            Debug.LogWarning("Debug mode is disabled");
            return;
        }
        
        Debug.Log("Testing promotion animation...");
        
        if (RankedManager.Instance != null)
        {
            var currentReal = RankedManager.Instance.GetPlayerRankData();
            previousRankData = currentReal;
            
            currentRankData = currentReal;
            if (currentReal.currentClass > 1)
            {
                currentRankData.currentClass = (byte)(currentReal.currentClass - 1);
                currentRankData.currentDiamonds = 1;
            }
            else if (currentReal.currentRankIndex < 5)
            {
                currentRankData.currentRankIndex = (byte)(currentReal.currentRankIndex + 1);
                currentRankData.currentClass = 3;
                currentRankData.currentDiamonds = 1;
            }
        }
        
        DebugReplayAnimation();
    }

    [ContextMenu("Test Demotion Animation")]
    private void DebugDemotionAnimation()
    {
        if (!enableDebugMode)
        {
            Debug.LogWarning("Debug mode is disabled");
            return;
        }
        
        Debug.Log("Testing demotion animation...");
        
        if (RankedManager.Instance != null)
        {
            var currentReal = RankedManager.Instance.GetPlayerRankData();
            previousRankData = currentReal;
            
            currentRankData = currentReal;
            var currentRankConfig = RankedSystemConfig.Instance.GetRankConfig(currentReal.currentRankIndex);
            
            if (currentReal.currentClass < currentRankConfig.totalClasses)
            {
                currentRankData.currentClass = (byte)(currentReal.currentClass + 1);
                currentRankData.currentDiamonds = (ushort)(currentRankConfig.diamondsToPromote - 1);
            }
            else if (currentReal.currentRankIndex > 0)
            {
                currentRankData.currentRankIndex = (byte)(currentReal.currentRankIndex - 1);
                currentRankData.currentClass = 1;
                var newRankConfig = RankedSystemConfig.Instance.GetRankConfig(currentRankData.currentRankIndex);
                currentRankData.currentDiamonds = (ushort)newRankConfig.diamondsToPromote;
            }
        }
        
        DebugReplayAnimation();
    }

    [ContextMenu("Test Diamond Gain")]
    private void DebugDiamondGain()
    {
        if (!enableDebugMode)
        {
            Debug.LogWarning("Debug mode is disabled");
            return;
        }
        
        Debug.Log("Testing diamond gain animation...");
        
        if (RankedManager.Instance != null)
        {
            var currentReal = RankedManager.Instance.GetPlayerRankData();
            previousRankData = currentReal;
            
            currentRankData = currentReal;
            currentRankData.currentDiamonds = (ushort)(currentReal.currentDiamonds + 1);
        }
        
        DebugReplayAnimation();
    }

    [ContextMenu("Test Diamond Loss")]
    private void DebugDiamondLoss()
    {
        if (!enableDebugMode)
        {
            Debug.LogWarning("Debug mode is disabled");
            return;
        }
        
        Debug.Log("Testing diamond loss animation...");
        
        if (RankedManager.Instance != null)
        {
            var currentReal = RankedManager.Instance.GetPlayerRankData();
            previousRankData = currentReal;
            
            currentRankData = currentReal;
            currentRankData.currentDiamonds = (ushort)Mathf.Max(0, currentReal.currentDiamonds - 1);
        }
        
        DebugReplayAnimation();
    }

    [ContextMenu("Test Promotion to Master")]
    private void DebugPromotionToMaster()
    {
        if (!enableDebugMode)
        {
            Debug.LogWarning("Debug mode is disabled");
            return;
        }
        
        Debug.Log("Testing promotion to master animation...");
        
        if (RankedManager.Instance != null)
        {
            var currentReal = RankedManager.Instance.GetPlayerRankData();
            previousRankData = currentReal;
            
            currentRankData = currentReal;
            for (byte i = 0; i < RankedSystemConfig.Instance.GetTotalRanks(); i++)
            {
                var rankConfig = RankedSystemConfig.Instance.GetRankConfig(i);
                if (rankConfig.isSpecialRank)
                {
                    currentRankData.currentRankIndex = i;
                    currentRankData.currentClass = 1;
                    currentRankData.currentDiamonds = 15;
                    break;
                }
            }
        }
        
        DebugReplayAnimation();
    }

    [ContextMenu("Test Master Diamond Gain")]
    private void DebugMasterDiamondGain()
    {
        if (!enableDebugMode)
        {
            Debug.LogWarning("Debug mode is disabled");
            return;
        }
        
        Debug.Log("Testing master diamond gain animation...");
        
        if (RankedManager.Instance != null)
        {
            for (byte i = 0; i < RankedSystemConfig.Instance.GetTotalRanks(); i++)
            {
                var rankConfig = RankedSystemConfig.Instance.GetRankConfig(i);
                if (rankConfig.isSpecialRank)
                {
                    previousRankData = new PlayerRankData
                    {
                        currentRankIndex = i,
                        currentClass = 1,
                        currentDiamonds = 15
                    };
                    
                    currentRankData = new PlayerRankData
                    {
                        currentRankIndex = i,
                        currentClass = 1,
                        currentDiamonds = 17
                    };
                    break;
                }
            }
        }
        
        DebugReplayAnimation();
    }

    [ContextMenu("Test Master Diamond Loss")]
    private void DebugMasterDiamondLoss()
    {
        if (!enableDebugMode)
        {
            Debug.LogWarning("Debug mode is disabled");
            return;
        }
        
        Debug.Log("Testing master diamond loss animation...");
        
        if (RankedManager.Instance != null)
        {
            for (byte i = 0; i < RankedSystemConfig.Instance.GetTotalRanks(); i++)
            {
                var rankConfig = RankedSystemConfig.Instance.GetRankConfig(i);
                if (rankConfig.isSpecialRank)
                {
                    previousRankData = new PlayerRankData
                    {
                        currentRankIndex = i,
                        currentClass = 1,
                        currentDiamonds = 15
                    };
                    
                    currentRankData = new PlayerRankData
                    {
                        currentRankIndex = i,
                        currentClass = 1,
                        currentDiamonds = 12
                    };
                    break;
                }
            }
        }
        
        DebugReplayAnimation();
    }
}