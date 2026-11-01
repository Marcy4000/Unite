using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    public SylveonCharacterRuntime SylveonRuntime => Combat?.Character as SylveonCharacterRuntime;
    public byte SylveonAttackCharge => SylveonRuntime?.Charge ?? 0;
    public byte SylveonPassiveStacks => SylveonRuntime?.PassiveStacks ?? 0;

    public CombatActor EnsureSylveonCombatRuntime(PlayerManager actorOverride = null) => Combat;

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void SylveonSnapshotRPC(
        byte charge,
        byte passiveStacks,
        double passiveUntil,
        bool movementLocked,
        bool hyperVoiceActive,
        double hyperVoiceUntil,
        Vector3 hyperVoiceDirection,
        long revision)
    {
        SylveonRuntime?.ReceiveSnapshot(
            charge,
            passiveStacks,
            passiveUntil,
            movementLocked,
            hyperVoiceActive,
            hyperVoiceUntil,
            hyperVoiceDirection,
            revision);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void SylveonDashRPC(Vector3 origin, Vector3 destination, double started, float duration, bool active, bool completed)
    {
        SylveonRuntime?.ReceiveDash(origin, destination, started, duration, active, completed);
    }
}
