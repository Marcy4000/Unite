using JSAM;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
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

    [SerializeField] private Button returnLobbyButton;

    private int blueScoreValue;
    private int orangeScoreValue;

    private void Start()
    {
        gameInfoUI.Initialize();
        battleInfoUI.Initialize(LobbyController.Instance.GameResults);
        blueScoreText.gameObject.SetActive(false);
        orangeScoreText.gameObject.SetActive(false);

        returnLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.StopMusic(DefaultAudioMusic.GameEnd);
            LobbyController.Instance.ReturnToLobby();
        });

        LoadingScreen.Instance.HideGenericLoadingScreen();

        ShowScore();
    }

    public void ShowScore()
    {
        resultBarsUI.gameObject.SetActive(true);
        gameInfoUI.gameObject.SetActive(false);


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
        resultBarsUI.gameObject.SetActive(false);
        gameInfoUI.gameObject.SetActive(true);

        blueScoreText.gameObject.SetActive(true);
        orangeScoreText.gameObject.SetActive(true);

        timerObject.SetActive(false);

        blueScoreText.text = blueScoreValue.ToString();
        orangeScoreText.text = orangeScoreValue.ToString();

        bool localPlayerTeam = LobbyController.Instance.Player.Data["PlayerTeam"].Value == "Orange";
        bool gameWon = LobbyController.Instance.GameResults.BlueTeamWon == !localPlayerTeam;
        StartCoroutine(PlayResultSound(gameWon));
    }

    private IEnumerator PlayResultSound(bool gameWon)
    {
        if (gameWon)
        {
            AudioManager.PlaySound(DefaultAudioSounds.JingleVictory);
        }
        else
        {
            AudioManager.PlaySound(DefaultAudioSounds.JingleLose);
        }

        DefaultAudioSounds jingle = gameWon ? DefaultAudioSounds.JingleVictory : DefaultAudioSounds.JingleLose;

        AudioManager.TryGetPlayingSound(jingle, out SoundChannelHelper helper);
        while (helper.AudioSource.isPlaying)
        {
            yield return null;
        }

        AudioManager.PlayMusic(DefaultAudioMusic.GameEnd);
    }
}
