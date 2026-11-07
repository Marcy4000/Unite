using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RacePositionsHolderUI : MonoBehaviour
{
    [SerializeField] private GameObject prefab;

    private List<RacePositionUI> racePositionUIs = new List<RacePositionUI>();

    private void Start()
    {
        RaceManager.Instance.OnInitializedPlayers += InitializePlayers;
        InitializePlayers();
    }

    private void InitializePlayers()
    {
        foreach (var raceUI in racePositionUIs)
        {
            if (raceUI.RaceLapCounter != null) raceUI.RaceLapCounter.OnPlaceChanged -= OnPlaceChanged;
            Destroy(raceUI.gameObject);
        }

        racePositionUIs.Clear();

        foreach (var player in RaceManager.Instance.PlayerLapCounters)
        {
            GameObject spawnedObject = Instantiate(prefab, transform);
            RacePositionUI racePositionUI = spawnedObject.GetComponent<RacePositionUI>();

            racePositionUI.AssignPlayer(player.Value);
            racePositionUI.RaceLapCounter.OnPlaceChanged += OnPlaceChanged;

            racePositionUIs.Add(racePositionUI);
        }

        ReorderRacePositions();
    }

    private void OnPlaceChanged(short position) => ReorderRacePositions();

    private void OnDestroy()
    {
        if (RaceManager.Instance != null) RaceManager.Instance.OnInitializedPlayers -= InitializePlayers;
        foreach (var ui in racePositionUIs)
            if (ui != null && ui.RaceLapCounter != null) ui.RaceLapCounter.OnPlaceChanged -= OnPlaceChanged;
    }

    private void ReorderRacePositions()
    {
        // Sort the list based on the CurrentPlace property of each RaceLapCounter
        racePositionUIs.Sort((a, b) => a.RaceLapCounter.CurrentPlace.CompareTo(b.RaceLapCounter.CurrentPlace));

        // Update the sibling index of each UI element to reflect the new order
        for (int i = 0; i < racePositionUIs.Count; i++)
        {
            racePositionUIs[i].transform.SetSiblingIndex(i);
        }
    }
}