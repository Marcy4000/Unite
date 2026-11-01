using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void FlareonServerPositionRPC(long castId, int sequence, Vector3 position, Vector3 direction)
    {
        if (!IsServer && Combat?.Character is FlareonCharacterRuntime runtime)
            runtime.ReceiveTraversalPosition(castId, sequence, position, direction);
    }
}
