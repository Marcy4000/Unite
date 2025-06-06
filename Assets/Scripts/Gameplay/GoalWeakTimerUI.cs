using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GoalWeakTimerUI : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private GameObject holder;

    [SerializeField] private Sprite orangeIcon;

    [SerializeField] private GoalZone goalZone;

    private void Start()
    {
        goalZone.onGoalStatusChanged += OnGoalStatusChanged;
        holder.SetActive(false);

        if (goalZone.Team == Team.Orange)
        {
            portraitImage.sprite = orangeIcon;
        }
    }

    private void OnGoalStatusChanged(GoalStatus goalStatus)
    {
        if (goalStatus == GoalStatus.Weakened)
        {
            holder.SetActive(true);
            StartCoroutine(UpdateTimer());
        }
        else
        {
            StopAllCoroutines();
            timerText.text = string.Empty;
            holder.SetActive(false);
        }
    }

    private IEnumerator UpdateTimer()
    {
        while (goalZone.WeakenTime > 0)
        {
            timerText.text = goalZone.WeakenTime.ToString("F1");
            yield return null;
        }
    }
}
