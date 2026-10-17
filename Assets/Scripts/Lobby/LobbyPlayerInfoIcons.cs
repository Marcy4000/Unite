using TMPro;
using UnityEngine;
using Player = Unite.Multiplayer.GameSessionPlayer;

public class LobbyPlayerInfoIcons : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private PlayerHeadUI playerHeadUI;

    public void Initialize(Player player)
    {
        playerNameText.text = player.Data["PlayerName"].Value;

        playerHeadUI.InitializeHead(PlayerClothesInfo.Deserialize(player.Data["ClothingInfo"].Value));
    }
}
