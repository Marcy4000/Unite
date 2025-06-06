using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerIcon : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private PlayerHeadUI playerHead;
    [SerializeField] private Button switchButton;
    [SerializeField] private Button kickButton;
    [SerializeField] private GameObject ownerStar;
    [SerializeField] private GameObject calculatingText;

    private Player assignedPlayer;

    private bool orangeTeam = false;
    private short position = 0;

    public bool OrangeTeam => orangeTeam;
    public short Position => position;

    public Button SwitchButton => switchButton;
    public Button KickButton => kickButton;

    public string PlayerName => playerNameText.text;
    public string PlayerId => assignedPlayer?.Id;

    public void InitializeElement(bool orangeTeam, short position)
    {
        this.orangeTeam = orangeTeam;
        this.position = position;
    }

    public void ResetName()
    {
        assignedPlayer = null;
        playerNameText.text = "No Player";
        playerHead.gameObject.SetActive(false);
        playerNameText.color = Color.white;
        ownerStar.SetActive(false);
        kickButton.gameObject.SetActive(false);
        kickButton.onClick.RemoveAllListeners();
        calculatingText.SetActive(false);
    }

    public void InitializePlayer(Player player)
    {
        assignedPlayer = player;
        playerNameText.text = player.Data["PlayerName"].Value;
        playerHead.gameObject.SetActive(true);
        playerHead.InitializeHead(PlayerClothesInfo.Deserialize(player.Data["ClothingInfo"].Value));

        ownerStar.SetActive(player.Id == LobbyController.Instance.Lobby.HostId);
        kickButton.gameObject.SetActive(player.Id != LobbyController.Instance.Player.Id && LobbyController.Instance.Lobby.HostId == LobbyController.Instance.Player.Id);

        playerNameText.color = player.Id == LobbyController.Instance.Player.Id ? Color.yellow : Color.white;

        kickButton.onClick.AddListener(() => LobbyController.Instance.KickPlayer(player.Id));

        calculatingText.SetActive(LobbyController.Instance.IsPlayerInResultScreen(player));
    }
}
