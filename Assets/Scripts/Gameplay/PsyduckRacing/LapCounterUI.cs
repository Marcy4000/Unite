using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LapCounterUI : MonoBehaviour
{
    [SerializeField] private TMP_Text lapCounterText;

    private bool initialized = false;
    private RaceManager raceManager;
    private RaceLapCounter localCounter;

    private void Start()
    {
        raceManager = RaceManager.Instance;
        raceManager.OnInitializedPlayers += OnInitializedPlayers;
    }

    private void OnInitializedPlayers()
    {
        if (initialized)
        {
            return;
        }

        foreach (var player in RaceManager.Instance.PlayerLapCounters)
        {
            NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[player.Key];

            if (playerNetworkObject.TryGetComponent(out PlayerManager actor) && actor.IsLocalHuman)
            {
                player.Value.OnLapChanged += OnLapChanged;
                localCounter = player.Value;
                OnLapChanged(player.Value.LapCount);

                initialized = true;
                break;
            }
        }
    }

    private void OnLapChanged(short lapCount)
    {
        lapCounterText.text = $"<color=\"yellow\">{lapCount}<color=\"white\"><size=50%> / {RaceManager.TOTAL_LAPS}";
    }
    private void OnDestroy()
    {
        if (raceManager != null) raceManager.OnInitializedPlayers -= OnInitializedPlayers;
        if (localCounter != null) localCounter.OnLapChanged -= OnLapChanged;
    }
}
