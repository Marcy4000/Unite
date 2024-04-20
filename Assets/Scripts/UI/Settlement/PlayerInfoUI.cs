using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Lobbies.Models;

public class PlayerInfoUI : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text playerScoreText;
    [SerializeField] private TMP_Text playerKillsText;
    [SerializeField] private TMP_Text playerAssistsText;
    [SerializeField] private TMP_Text playerBattleScoreText;

    [SerializeField] private Image playerAvatarImage;
    [SerializeField] private Image playerBGImage;

    [SerializeField] private Sprite blueBG, orangeBG;

    public void SetPlayerInfo(Player player)
    {
        playerNameText.text = player.Data["PlayerName"].Value;
        playerScoreText.text = "0";
        playerKillsText.text = "0";
        playerAssistsText.text = "0";
        playerBattleScoreText.text = "0";
        playerAvatarImage.sprite = CharactersList.instance.GetCharacterFromString(player.Data["SelectedCharacter"].Value).icon;

        if (player.Data["PlayerTeam"].Value == "Blue")
        {
            playerBGImage.sprite = blueBG;
        }
        else
        {
            playerBGImage.sprite = orangeBG;
        }
    }
}
