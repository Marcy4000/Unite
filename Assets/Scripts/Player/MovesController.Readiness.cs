using Unity.Netcode;

public partial class MovesController
{
    private bool allyUniteReady, sentUniteReady, sentUniteState;
    public bool UniteIsReady => IsOwner ? uniteMoveMaxCharge > 0 && uniteMoveCharge >= uniteMoveMaxCharge : allyUniteReady;

    private void TickUniteReadiness()
    {
        if (!IsSpawned || !IsOwner || playerManager == null) return;
        bool ready = UniteIsReady;
        if (sentUniteState && ready == sentUniteReady) return;
        sentUniteState = true;
        sentUniteReady = ready;
        ReportUniteReadyRpc(ready);
    }

    [Rpc(SendTo.Server)]
    private void ReportUniteReadyRpc(bool ready)
    {
        allyUniteReady = ready;
        var actor = GetComponent<PlayerManager>();
        if (actor == null || GameManager.Instance == null) return;
        foreach (var other in GameManager.Instance.Players)
            if (other != null && !other.IsBot && other.CurrentTeam.Team == actor.CurrentTeam.Team)
                ReceiveUniteReadyRpc(ready, RpcTarget.Single(other.OwnerClientId, RpcTargetUse.Temp));
        foreach (var observer in LobbyController.Instance.PlayerNetworkManagers)
            if (observer != null && observer.IsSpawned && observer.IsSpectator)
                ReceiveUniteReadyRpc(ready, RpcTarget.Single(observer.OwnerClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestUniteReadyRpc(RpcParams rpcParams = default)
    {
        var actor = GetComponent<PlayerManager>();
        if (actor == null || GameManager.Instance == null) return;
        var observer = LobbyController.Instance.PlayerNetworkManagers.Find(manager => manager != null && manager.IsSpawned &&
            manager.OwnerClientId == rpcParams.Receive.SenderClientId && manager.IsSpectator);
        if (observer != null)
        {
            ReceiveUniteReadyRpc(allyUniteReady, RpcTarget.Single(observer.OwnerClientId, RpcTargetUse.Temp));
            return;
        }
        foreach (var other in GameManager.Instance.Players)
            if (other != null && !other.IsBot && other.OwnerClientId == rpcParams.Receive.SenderClientId &&
                other.CurrentTeam.Team == actor.CurrentTeam.Team)
            {
                ReceiveUniteReadyRpc(allyUniteReady, RpcTarget.Single(other.OwnerClientId, RpcTargetUse.Temp));
                break;
            }
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void ReceiveUniteReadyRpc(bool ready, RpcParams rpcParams = default) => allyUniteReady = ready;
}
