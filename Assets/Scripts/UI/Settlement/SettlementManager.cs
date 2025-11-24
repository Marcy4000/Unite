using JSAM;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using DG.Tweening;
using System.Linq;

public class SettlementManager : MonoBehaviour
{
    [SerializeField] private ResultBarsUI resultBarsUI;
    [SerializeField] private GameInfoUI gameInfoUI;
    [SerializeField] private BattleInfoUI battleInfoUI;

    [SerializeField] private TMP_Text blueScoreText;
    [SerializeField] private TMP_Text orangeScoreText;

    [SerializeField] private GameObject timerObject;
    [SerializeField] private TMP_Text timerText;

    [SerializeField] private Image blueScoreboard, orangeScoreboard;
    [SerializeField] private Image blueResultText, orangeResultText;

    [SerializeField] private Sprite[] blueTeamResults;
    [SerializeField] private Sprite[] orangeTeamResults;

    [SerializeField] private Button returnLobbyButton;
    [SerializeField] private Button returnMainMenu;
    [SerializeField] private GameObject continueButton;
    [SerializeField] private GameObject mainUI;

    [SerializeField] private SettlementTeamModels settlementTeamModels;
    [SerializeField] private TeamPlayersMenu teamPlayersMenu;
    [SerializeField] private RankedResultsMenu rankedResultsMenu;

    private AsyncOperationHandle<Sprite> blueScoreboardHandle;
    private AsyncOperationHandle<Sprite> orangeScoreboardHandle;

    private int blueScoreValue;
    private int orangeScoreValue;

    private bool gameWon;
    private bool isRankedGame;
    private PlayerRankData previousRankData;

    private bool skipPointsAnimation = false;

    private IEnumerator Start()
    {
        gameInfoUI.Initialize();
        battleInfoUI.Initialize(LobbyController.Instance.GameResults);
        blueScoreText.gameObject.SetActive(false);
        orangeScoreText.gameObject.SetActive(false);

        blueResultText.gameObject.SetActive(false);
        orangeResultText.gameObject.SetActive(false);

        returnMainMenu.onClick.AddListener(() =>
        {
            AudioManager.StopMusic(DefaultAudioMusic.GameEnd);
            LobbyController.Instance.ReturnToLobby(true);
        });

        returnLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.StopMusic(DefaultAudioMusic.GameEnd);
            LobbyController.Instance.ReturnToHomeWithoutLeavingLobby();
        });

        settlementTeamModels.Initialize(LobbyController.Instance.GetTeamPlayers(LobbyController.Instance.GetLocalPlayerTeam()));
        teamPlayersMenu.Initialize(LobbyController.Instance.GetTeamPlayers(LobbyController.Instance.GetLocalPlayerTeam()));

        yield return InitializeScoreboards();

        LoadingScreen.Instance.HideGenericLoadingScreen();

        ShowScore();
    }

    private IEnumerator InitializeScoreboards()
    {
        MapInfo currentMap = CharactersList.Instance.GetCurrentLobbyMap();

        blueScoreboardHandle = Addressables.LoadAssetAsync<Sprite>(currentMap.mapResultsBlue);

        if (currentMap.customProperties.Any(prop => prop.key == "skipPointsAnimation"))
        {
            skipPointsAnimation = true;
        }

        yield return blueScoreboardHandle;

        if (blueScoreboardHandle.Status == AsyncOperationStatus.Succeeded)
        {
            blueScoreboard.gameObject.SetActive(true);
            blueScoreboard.sprite = blueScoreboardHandle.Result;
        }

        orangeScoreboardHandle = Addressables.LoadAssetAsync<Sprite>(currentMap.mapResultsOrange);

        yield return orangeScoreboardHandle;

        if (orangeScoreboardHandle.Status == AsyncOperationStatus.Succeeded)
        {
            orangeScoreboard.gameObject.SetActive(true);
            orangeScoreboard.sprite = orangeScoreboardHandle.Result;
        }
    }

    private void OnDestroy()
    {
        Addressables.Release(blueScoreboardHandle);
        Addressables.Release(orangeScoreboardHandle);
    }

    public void ShowScore()
    {
        resultBarsUI.gameObject.SetActive(true);
        gameInfoUI.gameObject.SetActive(false);
        continueButton.SetActive(false);

        blueScoreValue = LobbyController.Instance.GameResults.BlueTeamScore;
        orangeScoreValue = LobbyController.Instance.GameResults.OrangeTeamScore;

        UpdateTimerText(LobbyController.Instance.GameResults.TotalGameTime);

        int maxScore = blueScoreValue;
        if (orangeScoreValue > maxScore)
        {
            maxScore = orangeScoreValue;
        }

        resultBarsUI.InitializeUI(maxScore);

        if (skipPointsAnimation)
        {
            resultBarsUI.SetBars(blueScoreValue, orangeScoreValue);
            OnShowScoreEnded();
            return;
        }

        AnimateAppear(resultBarsUI.gameObject);

        StartCoroutine(ShowScoreRoutine());
    }

    private IEnumerator ShowScoreRoutine()
    {
        bool finished = false;
        int blueScoreValue = 0;
        int orangeScoreValue = 0;

        GameResults gameResults = LobbyController.Instance.GameResults;

        List<ResultScoreInfo> blueTeamScores = new List<ResultScoreInfo>(gameResults.BlueTeamScores);
        List<ResultScoreInfo> orangeTeamScores = new List<ResultScoreInfo>(gameResults.OrangeTeamScores);

        yield return new WaitForSeconds(1.5f);

        float totalTime = gameResults.TotalGameTime;
        Debug.Log($"Total Game Time: {totalTime}");

        float animationDuration = 3.5f;
        AudioManager.PlaySound(DefaultAudioSounds.Play_JieSuan_FenShu);

        float timeElapsed = 0f;

        while (!finished)
        {
            timeElapsed += Time.deltaTime;

            float interpolatedTime = Mathf.Lerp(0f, totalTime, timeElapsed / animationDuration);

            for (int i = blueTeamScores.Count - 1; i >= 0; i--)
            {
                if (blueTeamScores[i].Time <= interpolatedTime)
                {
                    blueScoreValue += blueTeamScores[i].ScoredPoints;
                    Debug.Log($"Blue Score {blueTeamScores[i].ScoredPoints} + At Time {blueTeamScores[i].Time} (Current time: {interpolatedTime})");
                    blueTeamScores.RemoveAt(i);
                }
            }

            for (int i = orangeTeamScores.Count - 1; i >= 0; i--)
            {
                if (orangeTeamScores[i].Time <= interpolatedTime)
                {
                    orangeScoreValue += orangeTeamScores[i].ScoredPoints;
                    Debug.Log($"Orange Score {orangeTeamScores[i].ScoredPoints} + At Time {orangeTeamScores[i].Time} (Current time: {interpolatedTime})");
                    orangeTeamScores.RemoveAt(i);
                }
            }

            UpdateTimerText(totalTime - interpolatedTime);

            resultBarsUI.SetBars(blueScoreValue, orangeScoreValue);

            if (timeElapsed >= animationDuration)
            {
                finished = true;
                UpdateTimerText(0f);
                yield return new WaitForSeconds(1f);
                OnShowScoreEnded();
            }

            yield return null;
        }
    }

    void UpdateTimerText(float timer)
    {
        int minutes = Mathf.FloorToInt(timer / 60f);
        int seconds = Mathf.FloorToInt(timer % 60f);
        timerText.text = string.Format("{0:00}m {1:00}s", minutes, seconds);
    }

    private void OnShowScoreEnded()
    {
        AudioManager.StopSound(DefaultAudioSounds.Play_JieSuan_FenShu);

        resultBarsUI.gameObject.SetActive(false);
        gameInfoUI.gameObject.SetActive(true);

        UpdateVictoryText();
        StartCoroutine(ShowScoreAndVictoryTextsWithDelay());

        timerObject.SetActive(false);

        blueScoreText.text = blueScoreValue.ToString();
        orangeScoreText.text = orangeScoreValue.ToString();

        Team localPlayerTeam = LobbyController.Instance.GetLocalPlayerTeam();
        gameWon = LobbyController.Instance.GameResults.WinningTeam == localPlayerTeam;
        
        ProcessRankedResults(gameWon);
        
        StartCoroutine(PlayResultSound(gameWon));
    }
    
    private void ProcessRankedResults(bool won)
    {
        if (RankedManager.Instance == null)
        {
            Debug.LogWarning("RankedManager not found, skipping rank processing");
            isRankedGame = false;
            return;
        }
        
        bool wasMatchmakingGame = false;
        if (LobbyController.Instance.Lobby != null && 
            LobbyController.Instance.Lobby.Data != null && 
            LobbyController.Instance.Lobby.Data.ContainsKey("IsMatchmakingLobby"))
        {
            wasMatchmakingGame = LobbyController.Instance.Lobby.Data["IsMatchmakingLobby"].Value == "true";
        }
        
        if (!wasMatchmakingGame && LobbyController.Instance.CurrentLobbyType == LobbyController.LobbyType.Standards)
        {
            wasMatchmakingGame = true;
        }
        
        isRankedGame = wasMatchmakingGame;
        
        if (isRankedGame)
        {
            previousRankData = RankedManager.Instance.GetPlayerRankData();
            
            RankedManager.Instance.ProcessMatchResult(won, wasMatchmakingGame);
            
            if (rankedResultsMenu != null)
            {
                var currentRankData = RankedManager.Instance.GetPlayerRankData();
                rankedResultsMenu.Initialize(this, previousRankData, currentRankData);
            }
        }
        
        Debug.Log($"Processing game result: Won={won}, WasMatchmaking={wasMatchmakingGame}");
    }

    private IEnumerator ShowScoreAndVictoryTextsWithDelay()
    {
        AnimateAppearInward(blueScoreText.gameObject);
        AnimateAppearInward(orangeScoreText.gameObject);

        yield return new WaitForSeconds(0.35f);

        AnimateAppearInward(blueResultText.gameObject);
        AnimateAppearInward(orangeResultText.gameObject);

        yield return new WaitForSeconds(0.15f);

        AnimateAppear(continueButton);
    }

    private void AnimateAppearInward(GameObject go)
    {
        go.SetActive(true);
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = go.AddComponent<CanvasGroup>();
        }
        cg.alpha = 0f;
        cg.transform.localScale = Vector3.one * 1.4f;
        cg.DOFade(1f, 0.45f).SetEase(Ease.OutQuad);
        cg.transform.DOScale(1f, 0.45f).SetEase(Ease.InBack);
    }

    private void AnimateAppear(GameObject go)
    {
        go.SetActive(true);
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = go.AddComponent<CanvasGroup>();
        }
        cg.alpha = 0f;
        cg.transform.localScale = Vector3.one * 0.7f;
        cg.DOFade(1f, 0.5f).SetEase(Ease.OutQuad);
        cg.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);
    }

    private void UpdateVictoryText()
    {
        GameResults gameResults = LobbyController.Instance.GameResults;

        if (gameResults.WinningTeam == Team.Blue)
        {
            blueResultText.sprite = blueTeamResults[0];
            if (gameResults.Surrendered)
            {
                orangeResultText.sprite = orangeTeamResults[2];
            }
            else
            {
                orangeResultText.sprite = orangeTeamResults[1];
            }
        }
        else
        {
            orangeResultText.sprite = orangeTeamResults[0];
            if (gameResults.Surrendered)
            {
                blueResultText.sprite = blueTeamResults[2];
            }
            else
            {
                blueResultText.sprite = blueTeamResults[1];
            }
        }

        if (LobbyController.Instance.GetLocalPlayerTeam() == Team.Blue)
        {
            teamPlayersMenu.SetGameResultImage(blueResultText.sprite);
        }
        else
        {
            teamPlayersMenu.SetGameResultImage(orangeResultText.sprite);
        }
    }

    private IEnumerator PlayResultSound(bool gameWon)
    {
        if (gameWon)
        {
            AudioManager.PlaySound(DefaultAudioSounds.JingleVictory);
            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerYouWin);
            AudioManager.PlaySound(DefaultAudioSounds.Play_JieSuan_ShengLi);
        }
        else
        {
            AudioManager.PlaySound(DefaultAudioSounds.JingleLose);
            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerYouLose);
            AudioManager.PlaySound(DefaultAudioSounds.Play_JieSuan_ShiBai);
        }

        DefaultAudioSounds jingle = gameWon ? DefaultAudioSounds.JingleVictory : DefaultAudioSounds.JingleLose;

        AudioManager.TryGetPlayingSound(jingle, out SoundChannelHelper helper);
        while (helper.AudioSource.isPlaying)
        {
            yield return null;
        }

        AudioManager.PlayMusic(DefaultAudioMusic.GameEnd, true);
    }

    public void GoToNextMenu()
    {
        if (isRankedGame && rankedResultsMenu != null)
        {
            mainUI.SetActive(false);
            rankedResultsMenu.ShowMenu();
        }
        else
        {
            GoToTeamMenu();
        }
    }

    public void GoToTeamMenu()
    {
        mainUI.SetActive(false);
        teamPlayersMenu.ShowMenu();

        if (gameWon)
            settlementTeamModels.PlayVictoryAnimations();
    }
}
