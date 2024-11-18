using TMPro;
using UnityEngine;

public class GameTimerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text timedModeTimerText;
    [SerializeField] private TMP_Text timedModeTimerTextFinalStretch;
    [SerializeField] private TMP_Text timelessModeTimerText;
    [SerializeField] private TMP_Text timelessModeTimerTextFinalStretch;
    [SerializeField] private GameObject timedModeTimerHolder;
    [SerializeField] private GameObject timedModeTimerHolderFinalStretch;
    [SerializeField] private GameObject timelessModeTimerHolder;
    [SerializeField] private GameObject timelessModeTimerHolderFinalStretch;

    [SerializeField] private TMP_Text[] blueTeamScoreTexts;
    [SerializeField] private TMP_Text[] orangeTeamScoreTexts;

    [SerializeField] private GameObject timedModeTimer, timelessModeTimer;

    private TMP_Text activeTimer;

    private void Start()
    {
        // Activate the appropriate UI elements based on the game mode
        bool isTimedMode = GameManager.Instance.CurrentMap.gameMode == GameMode.Timed;
        timedModeTimer.SetActive(isTimedMode);
        timelessModeTimer.SetActive(!isTimedMode);

        GameManager.Instance.onFinalStretch += () => SetActiveTimer(true);

        SetActiveTimer(false);
    }

    private void Update()
    {
        UpdateTimerText(GameManager.Instance.MAX_GAME_TIME, GameManager.Instance.GameTime, GameManager.Instance.CurrentMap.gameMode);
    }

    private void SetActiveTimer(bool isFinalStretch)
    {
        timedModeTimerHolder.SetActive(!isFinalStretch);
        timedModeTimerHolderFinalStretch.SetActive(isFinalStretch);
        timelessModeTimerHolder.SetActive(!isFinalStretch);
        timelessModeTimerHolderFinalStretch.SetActive(isFinalStretch);

        activeTimer = GetActiveTimerText(GameManager.Instance.CurrentMap.gameMode, isFinalStretch);
    }

    private TMP_Text GetActiveTimerText(GameMode gameMode, bool isFinalStretch)
    {
        if (gameMode == GameMode.Timed)
        {
            return isFinalStretch ? timedModeTimerTextFinalStretch : timedModeTimerText;
        }
        else
        {
            return isFinalStretch ? timelessModeTimerTextFinalStretch : timelessModeTimerText;
        }
    }

    private void UpdateTimerText(float maxTime, float currentTime, GameMode gameMode)
    {
        float displayTime = gameMode == GameMode.Timed ? maxTime - currentTime : currentTime;

        int minutes = Mathf.FloorToInt(displayTime / 60f);
        int seconds = Mathf.FloorToInt(displayTime % 60f);
        activeTimer.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        UpdateTeamScores();
    }

    private void UpdateTeamScores()
    {
        int orangeTeamScore = GameManager.Instance.OrangeTeamScore;
        int blueTeamScore = GameManager.Instance.BlueTeamScore;

        foreach (TMP_Text text in blueTeamScoreTexts)
        {
            text.text = blueTeamScore.ToString();
        }

        foreach (TMP_Text text in orangeTeamScoreTexts)
        {
            text.text = orangeTeamScore.ToString();
        }
    }
}