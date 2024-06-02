using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class PartyScreenUI : MonoBehaviour
{
    [SerializeField] private GameObject playerIconPrefab;
    [SerializeField] private GameObject playerIconHolder;
    [SerializeField] private GameObject startGameButton;
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private int maxPlayers = 10;
    private LobbyPlayerIcon[] playerIconsBlueTeam;
    private LobbyPlayerIcon[] playerIconsOrangeTeam;

    private void OnEnable()
    {
        LobbyController.Instance.onLobbyUpdate += UpdatePlayers;
    }

    private void OnDisable()
    {
        LobbyController.Instance.onLobbyUpdate -= UpdatePlayers;
    }

    public void InitializeUI(Lobby lobby)
    {
        int blueIndex = 0;
        int orangeIndex = 0;

        ClearUI();

        playerIconsBlueTeam = new LobbyPlayerIcon[maxPlayers/2];
        playerIconsOrangeTeam = new LobbyPlayerIcon[maxPlayers/2];
        lobbyCodeText.text = $"Lobby Code: {lobby.LobbyCode}";

        startGameButton.SetActive(lobby.HostId == AuthenticationService.Instance.PlayerId);

        for (int i = 0; i < maxPlayers; i++)
        {
            if (i < maxPlayers/2)
            {
                playerIconsBlueTeam[i] = Instantiate(playerIconPrefab, playerIconHolder.transform).GetComponent<LobbyPlayerIcon>();
            }
            else
            {
                playerIconsOrangeTeam[i - maxPlayers/2] = Instantiate(playerIconPrefab, playerIconHolder.transform).GetComponent<LobbyPlayerIcon>();
            }
        }

        for (int i = 0; i < lobby.Players.Count; i++)
        {
            if (lobby.Players[i].Data["PlayerTeam"].Value == "Blue")
            {
                playerIconsBlueTeam[blueIndex].Initialize(lobby.Players[i]);
                blueIndex++;
            }
            else
            {
                playerIconsOrangeTeam[orangeIndex].Initialize(lobby.Players[i]);
                orangeIndex++;
            }
        }
    }

    public void UpdatePlayers(Lobby lobby)
    {
        int blueIndex = 0;
        int orangeIndex = 0;
        startGameButton.SetActive(lobby.HostId == AuthenticationService.Instance.PlayerId);

        for (int i = 0; i < maxPlayers; i++)
        {
            if (i < maxPlayers / 2)
            {
                playerIconsBlueTeam[i].ResetName();
            }
            else
            {
                playerIconsOrangeTeam[i - maxPlayers / 2].ResetName();
            }
        }

        for (int i = 0; i < lobby.Players.Count; i++)
        {
            if (lobby.Players[i].Data["PlayerTeam"].Value == "Blue")
            {
                playerIconsBlueTeam[blueIndex].Initialize(lobby.Players[i]);
                blueIndex++;
            }
            else
            {
                playerIconsOrangeTeam[orangeIndex].Initialize(lobby.Players[i]);
                orangeIndex++;
            }
        }
    }

    public void TestStartGame()
    {
        LobbyController.Instance.StartGame();
    }

    public void CopyLobbyID()
    {
        GUIUtility.systemCopyBuffer = lobbyCodeText.text.Split(' ')[2];
    }

    public void SwitchTeam()
    {
        LobbyController.Instance.PlayerSwitchTeam();
    }

    public void ClearUI()
    {
        if (playerIconsBlueTeam == null || playerIconsOrangeTeam == null)
        {
            return;
        }

        for (int i = 0; i < maxPlayers; i++)
        {
            if (i < maxPlayers / 2)
            {
                Destroy(playerIconsBlueTeam[i].gameObject);
            }
            else
            {
                Destroy(playerIconsOrangeTeam[i - maxPlayers/2].gameObject);
            }
        }

        playerIconsBlueTeam = null;
        playerIconsOrangeTeam = null;
    }
}
