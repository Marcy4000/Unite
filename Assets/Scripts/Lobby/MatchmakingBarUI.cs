using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class MatchmakingBarUI : MonoBehaviour
{
    [SerializeField] private GameObject holder;
    [SerializeField] private TMP_Text estimatedTime, elapsedTime;
    [SerializeField] private Button cancelButton;

    private Action cancelAction;
    private float timeElapsed = 0f;
    private bool isSearching = false;

    public void SetCancelButtonAction(Action action)
    {
        cancelAction = action;
        cancelButton.onClick.RemoveAllListeners();
        cancelButton.onClick.AddListener(() =>
        {
            cancelAction?.Invoke();
            JSAM.AudioManager.PlaySound(DefaultAudioSounds.Home_ui_back_01);
        });
    }

    public void SetBarVisibility(bool isVisible)
    {
        holder.SetActive(isVisible);
        if (!isVisible)
        {
            isSearching = false;
            timeElapsed = 0f;
            SetElapsedTime("00:00");
        }
    }

    public void SetEstimatedTime(string time)
    {
        estimatedTime.text = time;
    }

    public void SetElapsedTime(string time)
    {
        elapsedTime.text = time;
    }

    public void StartSearching()
    {
        isSearching = true;
        timeElapsed = 0f;
        SetBarVisibility(true);
        SetEstimatedTime("Estimated time: N/A");
        SetElapsedTime("00:00");

        JSAM.AudioManager.PlaySound(DefaultAudioSounds.Home_Ui_Pipei);
    }

    public void StopSearching()
    {
        isSearching = false;
        SetBarVisibility(false);
    }

    private void Update()
    {
        if (isSearching)
        {
            timeElapsed += Time.deltaTime;
            int minutes = Mathf.FloorToInt(timeElapsed / 60);
            int seconds = Mathf.FloorToInt(timeElapsed % 60);
            string formattedTime = string.Format("{0:00}:{1:00}", minutes, seconds);
            SetElapsedTime(formattedTime);
        }
    }
}
