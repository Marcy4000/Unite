using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyPlayerIcon : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private Image playerHead;
    [SerializeField] private Button switchButton;

    private bool orangeTeam = false;
    private int position = 0;

    public bool OrangeTeam => orangeTeam;
    public int Position => position;

    public Button SwitchButton => switchButton;

    public string PlayerName => playerNameText.text;

    public void ResetName()
    {
        playerNameText.text = "No Player";
        playerHead.gameObject.SetActive(false);
        playerNameText.color = Color.white;
    }

    public void InitializeElement(bool orangeTeam, int position)
    {
        this.orangeTeam = orangeTeam;
        this.position = position;
    }

    public void InitializePlayer(Player player)
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
