using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Lobbies.Models;

public class DraftPlayerHolder : MonoBehaviour
{
    [SerializeField] private Transform playerIconsHolder;
    [SerializeField] private GameObject playerIconPrefab;

    [SerializeField] private bool orangeSide;

    private List<DraftPlayerIcon> playerIcons = new List<DraftPlayerIcon>();

    public bool OrangeSide => orangeSide;
    public List<DraftPlayerIcon> PlayerIcons => playerIcons;

    public void Initialize(List<Player> players)
    {
        foreach (Player player in players)
        {
            DraftPlayerIcon playerIcon = Instantiate(playerIconPrefab, playerIconsHolder).GetComponent<DraftPlayerIcon>();
            playerIcon.Initialize(player);
            playerIcons.Add(playerIcon);
        }
    }
}
