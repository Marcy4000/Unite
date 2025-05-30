using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class LapCounterUI : MonoBehaviour
{
    [SerializeField] private TMP_Text lapCounterText;

    private bool initialized = false;

    private void Start()
    {
        RaceManager.Instance.OnInitializedPlayers += OnInitializedPlayers;
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

            if (playerNetworkObject.OwnerClientId == NetworkManager.Singleton.LocalClientId)
            {
                player.Value.OnLapChanged += OnLapChanged;
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
}
