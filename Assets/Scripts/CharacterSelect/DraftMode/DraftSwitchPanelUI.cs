using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class DraftSwitchPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject switchPanel;
    [SerializeField] private Button confirmButton, rejectButton;
    [SerializeField] private TMP_Text pokemonName, playerName;
    [SerializeField] private Image timeBar, pokemonIcon;

    private readonly float SELECTION_TIME = 10;

    private float timeLeft;
    private Coroutine countdownCoroutine;

    public event System.Action OnConfirm;
    public event System.Action OnReject;

    public bool IsSelecting => switchPanel.activeSelf;

    private void Awake()
    {
        confirmButton.onClick.AddListener(() => OnConfirm?.Invoke());
        rejectButton.onClick.AddListener(() => OnReject?.Invoke());

        HideSwitchPanel();
    }

    public void ShowSwitchPanel(string senderID, short characterID, bool allowAccept)
    {
        if (countdownCoroutine != null)
        {
            return;
        }

        Player player = LobbyController.Instance.Lobby.Players.Find(p => p.Id == senderID);
        CharacterInfo character = CharactersList.Instance.GetCharacterFromID(characterID);

        playerName.text = player.Data["PlayerName"].Value;
        pokemonName.text = character.pokemonName;
        pokemonIcon.sprite = character.icon;
        timeLeft = SELECTION_TIME;
        timeBar.fillAmount = 1;
        switchPanel.SetActive(true);

        confirmButton.gameObject.SetActive(allowAccept);

        countdownCoroutine = StartCoroutine(Countdown());
    }

    public void HideSwitchPanel()
    {
        switchPanel.SetActive(false);
        StopAllCoroutines();
    }

    private IEnumerator Countdown()
    {
        while (timeLeft > 0)
        {
            timeLeft -= Time.deltaTime;
            timeBar.fillAmount = timeLeft / SELECTION_TIME;
            yield return null;
        }

        HideSwitchPanel();
        OnReject?.Invoke();

        countdownCoroutine = null;
    }
}
