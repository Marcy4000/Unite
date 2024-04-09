using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button startLobbyButton;
    [SerializeField] private Button exitLobbyButton;
    [SerializeField] private TMP_InputField lobbyIdField;

    [SerializeField] private GameObject[] lobbyUIs;
    [SerializeField] private PartyScreenUI partyScreenUI;

    private void Start()
    {
        startLobbyButton.onClick.AddListener(() =>
        {
            LobbyController.instance.CreateLobby();
        });

        lobbyIdField.onEndEdit.AddListener((string lobbyId) =>
        {
            if (string.IsNullOrWhiteSpace(lobbyId))
            {
                return;
            }
            LobbyController.instance.TryLobbyJoin(lobbyId.ToUpper());
        });

        exitLobbyButton.onClick.AddListener(() =>
        {
            LobbyController.instance.LeaveLobby();
        });

        ShowMainMenuUI();
    }

    public void ShowLobbyUI()
    {
        foreach (var lobbyUI in lobbyUIs)
        {
            lobbyUI.SetActive(false);
        }
        lobbyUIs[1].SetActive(true);
        partyScreenUI.InitializeUI(LobbyController.instance.Lobby);
    }

    public void ShowMainMenuUI()
    {
        foreach (var lobbyUI in lobbyUIs)
        {
            lobbyUI.SetActive(false);
        }
        lobbyUIs[0].SetActive(true);
    }

    public void UpdatePartyScreenUI()
    {
        partyScreenUI.UpdatePlayers(LobbyController.instance.Lobby);
    }
}
