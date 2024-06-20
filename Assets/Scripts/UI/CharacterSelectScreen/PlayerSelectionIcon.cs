using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSelectionIcon : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image characterIcon;
    [SerializeField] private Image battleItem;
    [SerializeField] private TMP_Text playerNameText;

    [SerializeField] private Sprite defaultBackground;

    private Player assignedPlayer;

    public void Initialize(Player player)
    {
        assignedPlayer = player;
        playerNameText.text = player.Data["PlayerName"].Value;
        if (assignedPlayer.Id == LobbyController.Instance.Player.Id)
        {
            playerNameText.color = Color.yellow;
        }
        characterIcon.gameObject.SetActive(false);
        LobbyController.Instance.onLobbyUpdate += UpdatePlayerData;
        UpdatePlayerData(LobbyController.Instance.Lobby);
    }

    private void OnDisable()
    {
        LobbyController.Instance.onLobbyUpdate -= UpdatePlayerData;
    }

    private void UpdatePlayerData(Lobby lobby)
    {
        assignedPlayer = lobby.Players.Find(x => x.Id == assignedPlayer.Id);

        CharacterInfo info = CharactersList.instance.GetCharacterFromString(assignedPlayer.Data["SelectedCharacter"].Value);

        if (info != null)
        {
            characterIcon.gameObject.SetActive(true);
            battleItem.gameObject.SetActive(true);
            background.sprite = CharactersList.instance.GetBackgroundFromClass(info.pClass);
            characterIcon.sprite = info.icon;
            battleItem.sprite = CharactersList.instance.GetBattleItemByID(int.Parse(assignedPlayer.Data["BattleItem"].Value)).icon;
        }
        else
        {
            background.sprite = defaultBackground;
            characterIcon.gameObject.SetActive(false);
            battleItem.gameObject.SetActive(false);
        }
    }
}
