using System.Collections.Generic;
using UnityEngine;

public class ScoreboardUI : MonoBehaviour
{
    [SerializeField] private GameObject playerItemPrefab;
    [SerializeField] private Transform blueList, orangeList;

    [SerializeField] private bool relativeMode = false;

    public void Initialize()
    {
        List<PlayerManager> players = GameManager.Instance.Players;

        Team localTeam = LobbyController.Instance.GetLocalPlayerTeam();
        foreach (PlayerManager player in players)
        {
            Transform parentList;
            if (relativeMode)
            {
                parentList = player.CurrentTeam.Team == localTeam ? blueList : orangeList;
            }
            else
            {
                parentList = player.CurrentTeam.Team == Team.Orange ? orangeList : blueList;
            }
            GameObject playerItem = Instantiate(playerItemPrefab, parentList);
            playerItem.GetComponent<ScoreboardPlayerItem>().SetPlayerInfo(player);
        }
    }

    public void OpenScoreboard()
    {
        gameObject.SetActive(true);
    }

    public void CloseScoreboard()
    {
        gameObject.SetActive(false);
    }
}
