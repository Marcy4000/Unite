using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyNameText, playerCountText, mapNameText;
    [SerializeField] private Image mapImage;

    [SerializeField] private Button joinButton;

    public void Initialize(Lobby lobby)
    {
        lobbyNameText.text = $"{lobby.Players.Where(p => p.Id == lobby.HostId).FirstOrDefault().Data["PlayerName"].Value}'s lobby";
        playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";
        MapInfo map = CharactersList.Instance.GetMapFromID(NumberEncoder.FromBase64<short>(lobby.Data["SelectedMap"].Value));

        mapNameText.text = map.mapName;
        mapImage.sprite = map.mapIcon;

        joinButton.onClick.AddListener(() =>
        {
            LobbyController.Instance.TryLobbyJoin(lobby.Id, true);
        });
    }
}
