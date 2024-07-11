using TMPro;
using UnityEngine;

public class GameTimerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text timerTextFinalStretch;
    [SerializeField] private GameObject timerHolder;
    [SerializeField] private GameObject timerHolderFinalStretch;

    private TMP_Text activeTimer;

    private void Start()
    {
        GameManager.Instance.onFinalStretch += () => SetActiveTimer(true);
        SetActiveTimer(false);
    }

    private void Update()
    {
        UpdateTimerText(GameManager.Instance.GameTime);
    }

    public void SetActiveTimer(bool finalStretch)
    {
        if (finalStretch)
        {
            timerHolder.SetActive(false);
            timerHolderFinalStretch.SetActive(true);
            activeTimer = timerTextFinalStretch;
        }
        else
        {
            timerHolder.SetActive(true);
            timerHolderFinalStretch.SetActive(false);
            activeTimer = timerText;
        }
    }

    void UpdateTimerText(float curr)
    {
        int minutes = Mathf.FloorToInt(curr / 60f);
        int seconds = Mathf.FloorToInt(curr % 60f);
        activeTimer.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
