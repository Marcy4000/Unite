using System.Collections.Generic;
using Unite.Multiplayer;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class LoadingManagerTracker
{
    private MatchTransitionBarrier selectionTransition;
    private NetworkSceneManager transitionSceneManager;
    private Unity.Netcode.NetworkManager transitionNetworkManager;
    private int nextSelectionTransition;
    private int receivedSelectionTransition;
    private float selectionTransitionDeadline;
    private const double TransitionLeadTime = 0.3;

    public bool BeginSelectionTransition(string sceneName)
    {
        if (!IsSpawned || !IsServer || selectionTransition != null ||
            !IsSelectionScene(sceneName) || NetworkManager.SceneManager == null)
            return false;

        selectionTransition = new MatchTransitionBarrier(++nextSelectionTransition, sceneName, NetworkManager.ConnectedClientsIds);
        selectionTransitionDeadline = Time.realtimeSinceStartup + 20f;
        transitionSceneManager = NetworkManager.SceneManager;
        transitionNetworkManager = NetworkManager;
        transitionSceneManager.OnLoadEventCompleted += OnSelectionSceneLoaded;
        transitionNetworkManager.OnClientDisconnectCallback += OnTransitionClientDisconnected;
        BeginSelectionTransitionRpc(selectionTransition.Generation, sceneName, NetworkManager.ServerTime.Time + TransitionLeadTime);
        return true;
    }

    public static bool IsSelectionScene(string sceneName) => sceneName == "CharacterSelect" ||
        sceneName == "DraftSelect" || sceneName == "RacingReadyScreen";

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void BeginSelectionTransitionRpc(int generation, string sceneName, double closeAt)
    {
        receivedSelectionTransition = generation;
        if (LoadingScreen.Instance != null)
            LoadingScreen.Instance.BeginSelectionTransition(generation, sceneName, closeAt, this);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SelectionClosedRpc(int generation, RpcParams rpcParams = default)
    {
        selectionTransition?.MarkClosed(generation, rpcParams.Receive.SenderClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SelectionScreenReadyRpc(int generation, RpcParams rpcParams = default)
    {
        selectionTransition?.MarkReady(generation, rpcParams.Receive.SenderClientId);
    }

    private void UpdateSelectionTransition()
    {
        if (!IsServer || !IsSpawned || selectionTransition == null) return;
        if (Time.realtimeSinceStartup >= selectionTransitionDeadline)
        {
            AbortServerTransition("Timed out waiting for clients to finish the match transition.", SessionFailureReason.LoadingTimedOut);
            return;
        }

        if (selectionTransition.StartLoading())
        {
            selectionTransitionDeadline = Time.realtimeSinceStartup + 90f;
            SceneEventProgressStatus status = transitionSceneManager.LoadScene(selectionTransition.SceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                AbortServerTransition($"Could not load the selection scene: {status}.", SessionFailureReason.ContentLoadFailed);
                return;
            }
        }

        if (selectionTransition != null && selectionTransition.ScheduleOpen())
        {
            OpenSelectionTransitionRpc(selectionTransition.Generation, NetworkManager.ServerTime.Time + TransitionLeadTime);
            ClearServerTransition();
        }
    }

    private void OnSelectionSceneLoaded(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
    {
        if (selectionTransition == null || selectionTransition.SceneName != sceneName) return;
        if (timedOut.Count > 0)
        {
            AbortServerTransition("Clients timed out loading the selection scene.", SessionFailureReason.LoadingTimedOut);
            return;
        }
        selectionTransition?.MarkSceneLoaded(sceneName);
    }

    private void OnTransitionClientDisconnected(ulong clientId) => selectionTransition?.RemoveClient(clientId);

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void OpenSelectionTransitionRpc(int generation, double openAt)
    {
        if (LoadingScreen.Instance != null)
            LoadingScreen.Instance.OpenSelectionTransition(generation, openAt);
    }

    private void AbortServerTransition(string reason, SessionFailureReason failure)
    {
        Debug.LogWarning($"[MatchTransition] {reason}", this);
        int generation = selectionTransition.Generation;
        ClearServerTransition();
        CancelSelectionTransitionRpc(generation, failure);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void CancelSelectionTransitionRpc(int generation, SessionFailureReason failure)
    {
        if (LoadingScreen.Instance != null)
            LoadingScreen.Instance.CancelSelectionTransition(generation);
        LobbyController.Instance?.ReportSessionFailure(failure);
    }

    private void ClearServerTransition()
    {
        if (transitionSceneManager != null)
            transitionSceneManager.OnLoadEventCompleted -= OnSelectionSceneLoaded;
        if (transitionNetworkManager != null)
            transitionNetworkManager.OnClientDisconnectCallback -= OnTransitionClientDisconnected;
        transitionSceneManager = null;
        transitionNetworkManager = null;
        selectionTransition = null;
    }

    private void ResetSelectionTransition()
    {
        ClearServerTransition();
        if (receivedSelectionTransition != 0 && LoadingScreen.Instance != null)
            LoadingScreen.Instance.CancelSelectionTransition(receivedSelectionTransition);
        receivedSelectionTransition = 0;
    }
}
