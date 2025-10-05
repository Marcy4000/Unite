using DG.Tweening;
using JSAM;
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

    [Space]
    [SerializeField] private bool showFinalSeconds;
    [SerializeField] private int finalSecondsThreshold = 5;
    [SerializeField] private GameObject finalSecondsHolder;
    [SerializeField] private TMP_Text finalSecondsText;
    [SerializeField] private GameObject timesUpImage;
    private int previousFinalSeconds = -1;

    private TMP_Text activeTimer;

    private void Start()
    {
        // Activate the appropriate UI elements based on the game mode
        bool isTimedMode = GameManager.Instance.CurrentMap.gameMode == GameMode.Timed;
        timedModeTimer.SetActive(isTimedMode);
        timelessModeTimer.SetActive(!isTimedMode);
        finalSecondsHolder.SetActive(false);
        timesUpImage.SetActive(false);

        GameManager.Instance.onFinalStretch += () => SetActiveTimer(true);

        SetActiveTimer(false);
    }

    private void Update()
    {
        if (showFinalSeconds)
        {
            float timeRemaining = GameManager.Instance.CurrentMap.gameMode == GameMode.Timed
                ? GameManager.Instance.MAX_GAME_TIME - GameManager.Instance.GameTime
                : GameManager.Instance.GameTime;
            if (timeRemaining <= finalSecondsThreshold && timeRemaining >= 0)
            {
                EnableFinalSecondsDisplay(true);
                int seconds = Mathf.CeilToInt(timeRemaining);
                if (seconds != previousFinalSeconds)
                {
                    // Play sound effect here
                    switch (seconds)
                    {
                        case 5:
                            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerFive);
                            break;
                        case 4:
                            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerFour);
                            break;
                        case 3:
                            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerThree);
                            break;
                        case 2:
                            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerTwo);
                            break;
                        case 1:
                            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerOne);
                            break;
                        case 0:
                            AudioManager.PlaySound(DefaultAudioSounds.TimesUp);
                            break;
                    }
                    PlayFinalSecondsPopInAnim(seconds == 0);
                    previousFinalSeconds = seconds;
                }
                finalSecondsText.text = seconds == 0 ? "" : seconds.ToString();
                if (seconds == 0)
                {
                    finalSecondsText.gameObject.SetActive(false);
                    timesUpImage.SetActive(true);
                }
                else
                {
                    finalSecondsText.gameObject.SetActive(true);
                    timesUpImage.SetActive(false);
                }
            }
            else
            {
                EnableFinalSecondsDisplay(false);
            }
        }

        UpdateTimerText(GameManager.Instance.MAX_GAME_TIME, GameManager.Instance.GameTime, GameManager.Instance.CurrentMap.gameMode);
    }

    private void PlayFinalSecondsPopInAnim(bool timeUp)
    {
        if (!timeUp)
        {
            finalSecondsHolder.transform.localScale = Vector3.one * 2.5f;
            finalSecondsHolder.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
        }
        else
        {
            finalSecondsHolder.transform.localScale = Vector3.one * 2f;
            finalSecondsHolder.transform.DOScale(Vector3.one * 0.85f, 0.35f).SetEase(Ease.OutBack).onComplete += () =>
            {
                finalSecondsHolder.transform.DOScale(Vector3.one, 0.15f);
            };
        }
    }

    private void SetActiveTimer(bool isFinalStretch)
    {
        timedModeTimerHolder.SetActive(!isFinalStretch);
        timedModeTimerHolderFinalStretch.SetActive(isFinalStretch);
        timelessModeTimerHolder.SetActive(!isFinalStretch);
        timelessModeTimerHolderFinalStretch.SetActive(isFinalStretch);

        activeTimer = GetActiveTimerText(GameManager.Instance.CurrentMap.gameMode, isFinalStretch);
    }

    private void EnableFinalSecondsDisplay(bool enable)
    {
        if (showFinalSeconds)
        {
            if (enable)
            {
                timedModeTimerHolder.SetActive(false);
                timedModeTimerHolderFinalStretch.SetActive(false);
                timelessModeTimerHolder.SetActive(false);
                timelessModeTimerHolderFinalStretch.SetActive(false);
            }
            else {
                SetActiveTimer(GameManager.Instance.FinalStretch);
            }
            finalSecondsHolder.SetActive(enable);
        }
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