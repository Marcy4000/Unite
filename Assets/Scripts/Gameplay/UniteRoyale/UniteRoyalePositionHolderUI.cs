using System.Collections.Generic;
using UnityEngine;

public class UniteRoyalePositionHolderUI : MonoBehaviour
{
    [SerializeField] private GameObject prefab;

    private List<UniteRoyalePositionUI> uniteRoyalePositionUIs = new List<UniteRoyalePositionUI>();

    private void Start()
    {
        UniteRoyaleManager.Instance.OnInitializedPlayers += InitializePlayers;
    }

    private void InitializePlayers()
    {
        foreach (var uniteRoyaleUI in uniteRoyalePositionUIs)
        {
            Destroy(uniteRoyaleUI.gameObject);
        }

        uniteRoyalePositionUIs.Clear();
        UniteRoyaleManager.Instance.OnPlayerStatsChanged += ReorderRacePositions;

        foreach (var player in UniteRoyaleManager.Instance.PlayersInGame)
        {
            GameObject spawnedObject = Instantiate(prefab, transform);
            UniteRoyalePositionUI uniteRoyalePositionUI = spawnedObject.GetComponent<UniteRoyalePositionUI>();

            uniteRoyalePositionUI.AssignPlayer(player);

            uniteRoyalePositionUIs.Add(uniteRoyalePositionUI);
        }

        ReorderRacePositions();
    }

    private void ReorderRacePositions()
    {
        uniteRoyalePositionUIs.Sort((a, b) => b.PlayerNetworkManager.PlayerStats.kills.CompareTo(a.PlayerNetworkManager.PlayerStats.kills));

        for (int i = 0; i < uniteRoyalePositionUIs.Count; i++)
        {
            uniteRoyalePositionUIs[i].SetCrownActive(i == 0);
            uniteRoyalePositionUIs[i].transform.SetSiblingIndex(i);
        }
    }
}
