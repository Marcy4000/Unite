using TMPro;
using UnityEngine;

public class ChatMessageUI : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private PlayerHeadUI playerHeadUI;

    public void SetMessage(ChatManager.ChatMessage message)
    {
        messageText.text = message.message;

        var player = LobbyController.Instance.Lobby.Players.Find(player => player.Id == message.senderID);

        if (player != null)
        {
            playerNameText.text = player.Data["PlayerName"].Value;
            playerHeadUI.InitializeHead(PlayerClothesInfo.Deserialize(player.Data["ClothingInfo"].Value));
        }
    }
}
