using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class RaceCheckpoint : NetworkBehaviour
{
    [SerializeField] private int checkpointIndex;
    [SerializeField] private bool isFinishLine;

    private int totalCheckpoints;

    public int CheckpointIndex => checkpointIndex;

    public event System.Action<ulong, int> onCheckpointReached;

    public override void OnNetworkSpawn()
    {
        totalCheckpoints = transform.parent.childCount-1;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out RaceLapCounter raceLapCounter))
        {
            if (raceLapCounter.CheckpointCount == checkpointIndex - 1 || (isFinishLine && raceLapCounter.CheckpointCount == totalCheckpoints))
            {
                if (isFinishLine && raceLapCounter.CheckpointCount == totalCheckpoints)
                {
                    raceLapCounter.IncrementLapCountRPC();
                }

                raceLapCounter.SetCheckpointCountRPC(checkpointIndex);

                onCheckpointReached?.Invoke(raceLapCounter.AssignedPlayerID, checkpointIndex);
                Debug.Log($"Player {raceLapCounter.AssignedPlayerID} has reached checkpoint {checkpointIndex} with {raceLapCounter.LapCount} laps; position {raceLapCounter.CurrentPlace}");
            }
            else if (raceLapCounter.CheckpointCount == checkpointIndex + 1)
            {
                raceLapCounter.SetCheckpointCountRPC(checkpointIndex);
                Debug.Log($"Player {raceLapCounter.AssignedPlayerID} is going backwards at checkpoint {checkpointIndex}");
            }
        }
    }
}
