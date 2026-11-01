using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void JolteonSnapshotRPC(string state)
    {
        (Combat?.Character as JolteonCharacterRuntime)?.ApplyPresentationState(state);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void JolteonSteeringRPC(long castId, Vector2 input)
    {
        (Combat?.Character as JolteonCharacterRuntime)?.ReceiveUniteSteering(castId, input);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void JolteonServerPositionRPC(long castId, int sequence, Vector3 position, Vector3 direction, bool active)
    {
        if (!IsServer && Combat?.Character is JolteonCharacterRuntime runtime)
            runtime.ReceiveTraversalPosition(castId, sequence, position, direction, active);
    }
}
