using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyPlayerIcon : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;

    public void ResetName()
    {
        playerNameText.text = "No Player";
    }

    public void Initialize(Player player)
    {
        playerNameText.text = player.Data["PlayerName"].Value;
    }
}
