using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SettlementManager : MonoBehaviour
{
    [SerializeField] private ResultBarsUI resultBarsUI;
    [SerializeField] private GameInfoUI gameInfoUI;

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
        blueScoreText.gameObject.SetActive(false);
        orangeScoreText.gameObject.SetActive(false);

        returnLobbyButton.onClick.AddListener(() =>
        {
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

        yield return new WaitForSeconds(1.5f);

        float timer = gameResults.TotalGameTime;
        Debug.Log($"Total Game Time: {timer}");

        while (!finished)
        {
            for (int i = gameResults.BlueTeamScores.Count-1; i >= 0; i--)
            {
                if (gameResults.BlueTeamScores[i].time >= timer)
                {
                    blueScoreValue += gameResults.BlueTeamScores[i].ScoredPoints;
                    Debug.Log($"Blue Score {gameResults.BlueTeamScores[i].ScoredPoints} + At Time {gameResults.BlueTeamScores[i].time} (Current time: {timer})");
                    gameResults.BlueTeamScores.RemoveAt(i);
                }
            }

            for (int i = gameResults.OrangeTeamScores.Count - 1; i >= 0; i--)
            {
                if (gameResults.OrangeTeamScores[i].time >= timer)
                {
                    orangeScoreValue += gameResults.OrangeTeamScores[i].ScoredPoints;
                    Debug.Log($"Orange Score {gameResults.OrangeTeamScores[i].ScoredPoints} + At Time {gameResults.OrangeTeamScores[i].time} (Current time: {timer})");
                    gameResults.OrangeTeamScores.RemoveAt(i);
                }
            }

            timer -= Time.deltaTime * 1000f;

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

            yield return new WaitForSeconds(0.05f);
        }
    }

    void UpdateTimerText(float timer)
    {
        int minutes = Mathf.FloorToInt(timer / 60f);
        int seconds = Mathf.FloorToInt(timer % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
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
    }
}
