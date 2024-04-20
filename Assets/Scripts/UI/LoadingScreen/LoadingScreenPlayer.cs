using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreenPlayer : MonoBehaviour
{
    [SerializeField] private Image portrait, playerBar;
    [SerializeField] private TMP_Text playerName, pokemonName;

    [SerializeField] private Sprite blueSprite, orangeSprite;
    [SerializeField] private GameObject localPlayerImage;

    public void SetPlayerData(Player player)
    {
        CharacterInfo info = CharactersList.instance.GetCharacterFromString(player.Data["SelectedCharacter"].Value);

        portrait.sprite = info.portrait;
        playerName.text = player.Data["PlayerName"].Value;
        pokemonName.text = info.pokemonName;
        bool orangeTeam = player.Data["PlayerTeam"].Value == "Orange";
        playerBar.sprite = orangeTeam ? orangeSprite : blueSprite;
        localPlayerImage.SetActive(player.Id == LobbyController.Instance.Player.Id);
    }
}
