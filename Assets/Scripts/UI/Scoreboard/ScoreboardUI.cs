using System.Collections.Generic;
using UnityEngine;

public class ScoreboardUI : MonoBehaviour
{
    [SerializeField] private GameObject playerItemPrefab;
    [SerializeField] private Transform blueList, orangeList;

    public void Initialize()
    {
        List<PlayerManager> players = GameManager.Instance.Players;

        foreach (PlayerManager player in players)
        {
            GameObject playerItem = Instantiate(playerItemPrefab, player.CurrentTeam.IsOnSameTeam(Team.Orange) ? orangeList : blueList);
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
