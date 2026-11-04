using Unity.Netcode;
using UnityEngine;

public partial class PlayerManager
{
    // Presentation state only: parking between volleyball rounds or after
    // finishing a race does not make the player dead or change their HP.
    private bool spectatorParked;
    private bool spectatorReturningFromParking;
    private Vector3 spectatorReturnPosition;

    [Rpc(SendTo.Everyone)]
    private void NotifySpectatorTeleportRpc(Vector3 destination, bool parked)
    {
        ApplySpectatorTeleportState(destination, parked);
    }

    private void ApplySpectatorTeleportState(Vector3 destination, bool parked)
    {
        spectatorReturningFromParking = !parked && (spectatorParked || spectatorReturningFromParking);
        spectatorReturnPosition = destination;
        spectatorParked = parked;
    }

    public bool TryGetSpectatorFollowPosition(out Vector3 position)
    {
        position = transform.position;
        if (spectatorParked || PlayerState == PlayerState.Dead || (pokemon != null && pokemon.CurrentHp <= 0)) return false;
        if (spectatorReturningFromParking)
        {
            // The return notification can precede transform interpolation on
            // another peer. Show the playable destination, never the flight up
            // from the below-map parking area. Resume normal following once
            // the replicated actor has reached the playable elevation.
            if (position.y < spectatorReturnPosition.y - 0.5f)
            {
                position = spectatorReturnPosition;
                return true;
            }
            spectatorReturningFromParking = false;
        }
        return true;
    }
}
