using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class LoadingManagerTracker : NetworkBehaviour
{
    [SerializeField]
    GameObject m_ProgressTrackerPrefab;

    /// <summary>
    /// Dictionary containing references to the NetworkedLoadingProgessTrackers that contain the loading progress of
    /// each client. Keys are ClientIds.
    /// </summary>
    public Dictionary<ulong, NetworkedLoadingProgressTracker> ProgressTrackers { get; } = new Dictionary<ulong, NetworkedLoadingProgressTracker>();

    /// <summary>
    /// This is the AsyncOperation of the current load operation. This property should be set each time a new
    /// loading operation begins.
    /// </summary>
    public AsyncOperation LocalLoadOperation
    {
        set
        {
            if (value == null)
            {
                Debug.LogWarning("[LoadingProgress] Cannot track a null scene load operation.", this);
                return;
            }
            m_IsLoading = true;
            LocalProgress = 0;
            m_LocalLoadOperation = value;
            m_LocalAddressableOperation = null;
        }
    }

    /// <summary>
    /// This is the AsyncOperationHandle of the current addressable load operation. This property should be set each time a new
    /// loading operation begins.
    /// </summary>
    public AsyncOperationHandle LocalAddressableOperation
    {
        set
        {
            if (!value.IsValid())
            {
                Debug.LogWarning("[LoadingProgress] Cannot track an invalid Addressables operation.", this);
                return;
            }
            m_IsLoading = true;
            LocalProgress = 0;
            m_LocalLoadOperation = null;
            m_LocalAddressableOperation = value;
        }
    }

    AsyncOperation m_LocalLoadOperation;
    AsyncOperationHandle? m_LocalAddressableOperation;

    float m_LocalProgress;

    bool m_IsLoading;
    NetworkManager m_SubscribedNetworkManager;

    /// <summary>
    /// This event is invoked each time the dictionary of progress trackers is updated (if one is removed or added, for example.)
    /// </summary>
    public event Action onTrackersUpdated;

    /// <summary>
    /// The current loading progress for the local client, buffered even before its tracker spawns.
    /// </summary>
    public float LocalProgress
    {
        get => m_LocalProgress;
        private set
        {
            // Always update local progress first
            m_LocalProgress = value;
            
            // Then sync to network if available
            if (IsSpawned && NetworkManager != null &&
                ProgressTrackers.TryGetValue(NetworkManager.LocalClientId, out var tracker) &&
                tracker != null && tracker.IsSpawned && tracker.IsOwner)
            {
                tracker.Progress.Value = value;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        NetworkObject.DestroyWithScene = false;
        NetworkedLoadingProgressTracker.Spawned += RegisterTracker;
        NetworkedLoadingProgressTracker.Despawned += UnregisterTracker;

        // Initial scene synchronization can spawn trackers before this manager.
        // Subsequent arrivals register themselves, rather than relying on RPC timing.
        foreach (var tracker in FindObjectsByType<NetworkedLoadingProgressTracker>(FindObjectsSortMode.None))
        {
            RegisterTracker(tracker);
        }

        if (IsServer)
        {
            m_SubscribedNetworkManager = NetworkManager;
            m_SubscribedNetworkManager.OnClientConnectedCallback += AddTracker;
            m_SubscribedNetworkManager.OnClientDisconnectCallback += RemoveTracker;
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                AddTracker(clientId);
            }
        }
    }
    public override void OnNetworkDespawn()
    {
        ResetSelectionTransition();
        UnsubscribeNetworkEvents();
        m_IsLoading = false;
        m_LocalLoadOperation = null;
        m_LocalAddressableOperation = null;
        m_LocalProgress = 0;
        ProgressTrackers.Clear();
        onTrackersUpdated?.Invoke();
    }

    public override void OnDestroy()
    {
        ResetSelectionTransition();
        UnsubscribeNetworkEvents();
        base.OnDestroy();
    }

    void UnsubscribeNetworkEvents()
    {
        NetworkedLoadingProgressTracker.Spawned -= RegisterTracker;
        NetworkedLoadingProgressTracker.Despawned -= UnregisterTracker;
        if (m_SubscribedNetworkManager != null)
        {
            m_SubscribedNetworkManager.OnClientConnectedCallback -= AddTracker;
            m_SubscribedNetworkManager.OnClientDisconnectCallback -= RemoveTracker;
            m_SubscribedNetworkManager = null;
        }
    }

    void Update()
    {
        UpdateSelectionTransition();
        if (m_IsLoading)
        {
            if (m_LocalLoadOperation != null)
            {
                if (m_LocalLoadOperation.isDone)
                {
                    m_IsLoading = false;
                    LocalProgress = 1;
                }
                else
                {
                    LocalProgress = m_LocalLoadOperation.progress;
                }
            }
            else if (m_LocalAddressableOperation.HasValue)
            {
                if (!m_LocalAddressableOperation.Value.IsValid())
                {
                    Debug.LogWarning("[LoadingProgress] The tracked Addressables operation was released before completion.", this);
                    m_IsLoading = false;
                    m_LocalAddressableOperation = null;
                    return;
                }
                if (m_LocalAddressableOperation.Value.IsDone)
                {
                    m_IsLoading = false;
                    LocalProgress = 1;
                }
                else
                {
                    LocalProgress = m_LocalAddressableOperation.Value.PercentComplete;
                }
            }
        }
    }

    void RegisterTracker(NetworkedLoadingProgressTracker tracker)
    {
        if (!IsSpawned || tracker == null || !tracker.IsSpawned || tracker.NetworkManager != NetworkManager)
        {
            return;
        }

        if (ProgressTrackers.TryGetValue(tracker.OwnerClientId, out var existing) && existing != null && existing.IsSpawned)
        {
            if (existing != tracker)
            {
                Debug.LogWarning($"[LoadingProgress] Ignoring duplicate tracker for client {tracker.OwnerClientId}.", tracker);
            }
            return;
        }

        ProgressTrackers[tracker.OwnerClientId] = tracker;
        if (tracker.IsOwner)
        {
            // Zero is meaningful too: it resets progress left over from the previous load.
            tracker.Progress.Value = m_LocalProgress;
        }
        onTrackersUpdated?.Invoke();
    }

    void UnregisterTracker(NetworkedLoadingProgressTracker tracker)
    {
        // A late despawn must not remove a replacement for the same owner.
        if (ProgressTrackers.TryGetValue(tracker.OwnerClientId, out var existing) && ReferenceEquals(existing, tracker))
        {
            ProgressTrackers.Remove(tracker.OwnerClientId);
            onTrackersUpdated?.Invoke();
        }
    }

    void AddTracker(ulong clientId)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        // Host startup also fires OnClientConnectedCallback after OnNetworkSpawn.
        if (ProgressTrackers.TryGetValue(clientId, out var existing) && existing != null && existing.IsSpawned)
        {
            return;
        }

        if (m_ProgressTrackerPrefab == null ||
            !m_ProgressTrackerPrefab.TryGetComponent<NetworkObject>(out _) ||
            !m_ProgressTrackerPrefab.TryGetComponent<NetworkedLoadingProgressTracker>(out _))
        {
            Debug.LogError("[LoadingProgress] Progress tracker prefab requires a NetworkObject and NetworkedLoadingProgressTracker.", this);
            return;
        }

        var tracker = Instantiate(m_ProgressTrackerPrefab);
        tracker.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);
    }

    void RemoveTracker(ulong clientId)
    {
        if (IsServer && ProgressTrackers.TryGetValue(clientId, out var tracker))
        {
            ProgressTrackers.Remove(clientId);
            if (tracker != null && tracker.IsSpawned)
            {
                tracker.NetworkObject.Despawn();
            }
            onTrackersUpdated?.Invoke();
        }
    }
}
