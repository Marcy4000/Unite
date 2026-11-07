using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RaceLapCounter : NetworkBehaviour
{
    private NetworkVariable<ulong> assignedPlayerID = new NetworkVariable<ulong>(0);

    private NetworkVariable<short> lapCount = new NetworkVariable<short>(0);
    private NetworkVariable<int> checkpointCount = new NetworkVariable<int>(0);

    private NetworkVariable<short> currentPlace = new NetworkVariable<short>(0);

    private NetworkVariable<bool> raceFinished = new NetworkVariable<bool>(false);

    public short LapCount => lapCount.Value;
    public int CheckpointCount => checkpointCount.Value;
    public short CurrentPlace => currentPlace.Value;

    public bool RaceFinished => raceFinished.Value;

    public ulong AssignedPlayerID => assignedPlayerID.Value;

    public event System.Action<short> OnPlaceChanged;
    public event System.Action<short> OnLapChanged;

    private Transform target;

    public override void OnNetworkSpawn()
    {
        currentPlace.OnValueChanged += (oldValue, newValue) =>
        {
            OnPlaceChanged?.Invoke(newValue);
        };

        lapCount.OnValueChanged += (oldValue, newValue) =>
        {
            OnLapChanged?.Invoke(newValue);
        };
    }

    [Rpc(SendTo.Server)]
    public void InitializeRPC(ulong targetID)
    {
        lapCount.Value = 1;
        checkpointCount.Value = 0;
        assignedPlayerID.Value = targetID;
        currentPlace.Value = 0;

        // The assigned ID is replicated; resolve the avatar when it arrives.
    }

    private void Update()
    {
        if (!IsSpawned || LapCount == 0) return;
        if (target == null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(AssignedPlayerID, out var avatar))
            target = avatar.transform;
        if (target != null) transform.position = target.position;
    }

    [Rpc(SendTo.Server)]
    public void SetCheckpointCountRPC(int count)
    {
        checkpointCount.Value = count;
    }

    [Rpc(SendTo.Server)]
    public void IncrementLapCountRPC()
    {
        lapCount.Value++;
    }

    [Rpc(SendTo.Server)]
    public void SetCurrentPlaceRPC(short place)
    {
        currentPlace.Value = place;
    }

    [Rpc(SendTo.Server)]
    public void SetRaceFinishedRPC(bool finished)
    {
        raceFinished.Value = finished;
    }
}
