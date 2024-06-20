using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerIcon : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image playerHead;

    public void ResetName()
    {
        playerNameText.text = "No Player";
        playerHead.gameObject.SetActive(false);
        playerNameText.color = Color.white;
    }

    public void Initialize(Player player)
    {
        playerNameText.text = player.Data["PlayerName"].Value;
        playerHead.gameObject.SetActive(true);
        if (player.Id == LobbyController.Instance.Player.Id)
        {
            playerNameText.color = Color.yellow;
        }
        else
        {
            playerNameText.color = Color.white;
        }

        // TODO: Let player decide which head it wants to use
    }
}
