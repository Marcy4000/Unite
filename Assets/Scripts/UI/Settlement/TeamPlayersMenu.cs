using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class TeamPlayersMenu : MonoBehaviour
{
    [SerializeField] private SettlementTeamPlayer[] teamPlayers;
    [SerializeField] private Image gameResultImage;

    private void Start()
    {
        HideMenu();
    }

    public void Initialize(Player[] players)
    {
        for (int i = 0; i < teamPlayers.Length; i++)
        {
            if (i < players.Length)
            {
                teamPlayers[i].Initialize(players[i]);
            }
            else
            {
                teamPlayers[i].Initialize(null);
            }
        }
    }

    public void SetGameResultImage(Sprite sprite)
    {
        gameResultImage.sprite = sprite;
    }

    public void ShowMenu()
    {
        gameObject.SetActive(true);
    }

    public void HideMenu()
    {
        gameObject.SetActive(false);
    }
}
