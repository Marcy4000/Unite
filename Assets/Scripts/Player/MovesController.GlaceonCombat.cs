using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void GlaceonDashRPC(Vector3 origin, Vector3 destination, double started, bool active, bool completed)
    {
        (Combat?.Character as GlaceonCharacterRuntime)?.ReceiveDash(origin, destination, started, active, completed);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void GlaceonSnapshotRPC(byte crystals, double expiresAt, byte charge, byte mode, bool spearLocked, bool shardLocked, bool movementLocked, bool flying, long revision)
    {
        (Combat?.Character as GlaceonCharacterRuntime)?.ReceiveSnapshot(crystals, expiresAt, charge, mode, spearLocked, shardLocked, movementLocked, flying, revision);
    }
}
