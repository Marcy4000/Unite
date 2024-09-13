using JSAM;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

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
    [SerializeField] private GameObject continueButton;
    [SerializeField] private GameObject mainUI;

    [SerializeField] private SettlementTeamModels settlementTeamModels;
    [SerializeField] private TeamPlayersMenu teamPlayersMenu;

    private AsyncOperationHandle<Sprite> blueScoreboardHandle;
    private AsyncOperationHandle<Sprite> orangeScoreboardHandle;

    private int blueScoreValue;
    private int orangeScoreValue;

    private bool gameWon;

    private IEnumerator Start()
    {
        gameInfoUI.Initialize();
        battleInfoUI.Initialize(LobbyController.Instance.GameResults);
        blueScoreText.gameObject.SetActive(false);
        orangeScoreText.gameObject.SetActive(false);

        blueResultText.gameObject.SetActive(false);
        orangeResultText.gameObject.SetActive(false);

        returnLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.StopMusic(DefaultAudioMusic.GameEnd);
            LobbyController.Instance.ReturnToLobby(true);
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

        float timer = gameResults.TotalGameTime;
        Debug.Log($"Total Game Time: {timer}");

        AudioManager.PlaySound(DefaultAudioSounds.Play_JieSuan_FenShu);

        while (!finished)
        {
            for (int i = blueTeamScores.Count-1; i >= 0; i--)
            {
                if (blueTeamScores[i].Time >= timer)
                {
                    blueScoreValue += blueTeamScores[i].ScoredPoints;
                    Debug.Log($"Blue Score {blueTeamScores[i].ScoredPoints} + At Time {blueTeamScores[i].Time} (Current time: {timer})");
                    blueTeamScores.RemoveAt(i);
                }
            }

            for (int i = orangeTeamScores.Count - 1; i >= 0; i--)
            {
                if (orangeTeamScores[i].Time >= timer)
                {
                    orangeScoreValue += orangeTeamScores[i].ScoredPoints;
                    Debug.Log($"Orange Score {orangeTeamScores[i].ScoredPoints} + At Time {orangeTeamScores[i].Time} (Current time: {timer})");
                    orangeTeamScores.RemoveAt(i);
                }
            }

            timer -= Time.deltaTime * 150f;

            UpdateTimerText(timer);

            resultBarsUI.SetBars(blueScoreValue, orangeScoreValue);

            if (timer <= 0)
            {
                finished = true;
                timer = 0;
                UpdateTimerText(timer);
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

        blueScoreText.gameObject.SetActive(true);
        orangeScoreText.gameObject.SetActive(true);

        blueResultText.gameObject.SetActive(true);
        orangeResultText.gameObject.SetActive(true);

        continueButton.SetActive(true);

        UpdateVictoryText();

        timerObject.SetActive(false);

        blueScoreText.text = blueScoreValue.ToString();
        orangeScoreText.text = orangeScoreValue.ToString();

        bool localPlayerTeam = LobbyController.Instance.GetLocalPlayerTeam();
        gameWon = LobbyController.Instance.GameResults.BlueTeamWon == !localPlayerTeam;
        StartCoroutine(PlayResultSound(gameWon));
    }

    private void UpdateVictoryText()
    {
        GameResults gameResults = LobbyController.Instance.GameResults;

        if (gameResults.BlueTeamWon)
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

        if (!LobbyController.Instance.GetLocalPlayerTeam())
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
        mainUI.SetActive(false);
        teamPlayersMenu.ShowMenu();

        if (gameWon)
            settlementTeamModels.PlayVictoryAnimations();
    }
}
