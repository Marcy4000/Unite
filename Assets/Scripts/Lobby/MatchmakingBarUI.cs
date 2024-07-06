using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MatchmakingBarUI : MonoBehaviour
{
    [SerializeField] private GameObject holder;
    [SerializeField] private TMP_Text estimatedTime, elapsedTime;

    [SerializeField] private Button cancelButton;

    public void SetCancelButtonAction(System.Action action)
    {
        cancelButton.onClick.AddListener(() => action());
    }

    public void SetBarVisibility(bool isVisible)
    {
        holder.SetActive(isVisible);
    }

    public void SetEstimatedTime(string time)
    {
        estimatedTime.text = time;
    }

    public void SetElapsedTime(string time)
    {
        elapsedTime.text = time;
    }
}
