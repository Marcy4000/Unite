using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

public class LobbyItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyNameText, playerCountText, mapNameText;
    [SerializeField] private Image mapImage;

    [SerializeField] private Button joinButton;

    public void Initialize(ISessionInfo lobby)
    {
        lobbyNameText.text = lobby.Properties.TryGetValue("HostDisplayName", out var hostName) ? $"{hostName.Value}'s lobby" : lobby.Name;
        playerCountText.text = $"{lobby.MaxPlayers - lobby.AvailableSlots}/{lobby.MaxPlayers}";
        MapInfo map = CharactersList.Instance.GetMapFromID(NumberEncoder.FromBase64<short>(lobby.Properties["SelectedMap"].Value));

        mapNameText.text = map.mapName;
        mapImage.sprite = map.mapIcon;

        joinButton.onClick.AddListener(() =>
        {
            LobbyController.Instance.TryLobbyJoin(lobby.Id, true);
        });
    }
}
