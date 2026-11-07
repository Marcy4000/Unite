using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unite.Multiplayer;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

public sealed class OpenWorldLobbyController : MonoBehaviour, IMigrationDataHandler
{
    [Serializable]
    public sealed class TrainerPosition
    {
        public string playerId;
        public Vector3 position;
        public float yaw;
    }

    [Serializable]
    private sealed class MigrationSnapshot
    {
        public int version = 1;
        public string worldVersion;
        public string cityId;
        public TrainerPosition[] trainers;
        public OpenWorldStageJukebox.Snapshot stage;
    }

    private readonly SemaphoreSlim operationGate = new SemaphoreSlim(1, 1);
    private readonly Dictionary<ulong, string> connections = new Dictionary<ulong, string>();
    private readonly Dictionary<ulong, float> connectionDeadlines = new Dictionary<ulong, float>();
    private readonly Dictionary<ulong, NetworkManager.ConnectionApprovalResponse> pendingApprovals =
        new Dictionary<ulong, NetworkManager.ConnectionApprovalResponse>();
    private readonly Dictionary<ulong, OpenWorldTrainerAvatar> avatars = new Dictionary<ulong, OpenWorldTrainerAvatar>();
    private readonly Dictionary<string, TrainerPosition> positions = new Dictionary<string, TrainerPosition>();
    private const float MemberConnectionGrace = 60f;
    private const float MemberCleanupRetry = 15f;
    private readonly Dictionary<string, float> absentMembers = new Dictionary<string, float>();
    private string removingMember;
    private ISession session;
    private Task leaveTask;
    private CancellationTokenSource connectionCancellation;
    private NetworkManager network;
    private OpenWorldLobbyView view;
    private GameSessionPlayer localPlayer;
    private GameSessionView chatLobby;
    private GameSessionPlayer chatLocalPlayer;
    private readonly Dictionary<string, PlayerProperty> chatPendingProperties = new Dictionary<string, PlayerProperty>();
    private GameObject originalPlayerPrefab;
    private byte[] originalConnectionData;
    private bool originalApproval;
    private bool configured;
    private bool leaving;
    private bool entering;
    private bool recovering;
    private bool migrationInProgress;
    private bool restoreLocalPose;
    private float disconnectedAt = -1f;
    private float nextRosterUpdate;
    private string retiredCityId;
    private Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse> originalApprovalCallback;
    private Action removed;
    private Action deleted;
    private Action<SessionError> migrationFailed;
    private Action<NetworkState> networkChanged;

    public bool IsActive => session != null && !leaving;
    public bool IsConnected => IsActive && network != null && network.IsConnectedClient &&
        session.Network.State == NetworkState.Started;
    public string Status { get; private set; } = "Connecting to city...";

    public GameSessionView ChatLobby
    {
        get
        {
            if (!IsActive || localPlayer == null) return null;
            if (chatLobby == null) chatLobby = new GameSessionView();
            if (chatLocalPlayer == null) chatLocalPlayer = new GameSessionPlayer(localPlayer.Id);
            chatLobby.Apply(session, chatLocalPlayer, chatPendingProperties);
            foreach (var member in chatLobby.Players) member.Data.Remove("OwnerID");
            if (network != null && network.IsListening && network.SpawnManager != null)
            {
                if (network.IsServer)
                {
                    foreach (var connection in connections)
                        if (network.ConnectedClients.ContainsKey(connection.Key))
                            SetChatOwner(connection.Value, connection.Key);
                }
                else foreach (var spawned in network.SpawnManager.SpawnedObjectsList)
                {
                    var trainer = spawned.GetComponent<OpenWorldTrainerAvatar>();
                    if (trainer == null || !trainer.IsSpawned) continue;
                    SetChatOwner(trainer.PlayerId, trainer.OwnerClientId);
                }
            }
            FilterConnectedChatPlayers(chatLobby);
            return chatLobby;
        }
    }

    private static void FilterConnectedChatPlayers(GameSessionView lobby)
    {
        lobby.Players.RemoveAll(member => !member.Data.ContainsKey("OwnerID"));
    }

    private void SetChatOwner(string playerId, ulong clientId)
    {
        var member = chatLobby.Players.Find(player => player.Id == playerId);
        if (member != null)
            member.Data["OwnerID"] = new PlayerProperty(clientId.ToString(), VisibilityPropertyOptions.Member);
    }

    public void CancelPendingConnection()
    {
        leaving = true;
        connectionCancellation?.Cancel();
    }

    public async Task EnterAsync(GameSessionPlayer player, OpenWorldLobbyView lobbyView)
    {
        await operationGate.WaitAsync();
        entering = true;
        leaving = false;
        try
        {
            await LeaveSessionAsync();
            if (leaving) throw new OperationCanceledException("City entry was canceled while the previous session was leaving.");
            localPlayer = player ?? throw new ArgumentNullException(nameof(player));
            view = lobbyView != null ? lobbyView : throw new ArgumentNullException(nameof(lobbyView));
            if (view.AvatarPrefab == null) throw new InvalidOperationException("The city has no trainer avatar prefab.");
            positions.Clear();
            restoreLocalPose = false;
            ConfigureNetwork();
            connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(Application.exitCancellationToken);
            connectionCancellation.CancelAfter(TimeSpan.FromSeconds(60));
            Status = "Finding a city...";
            var options = BuildOptions();
            var filters = OpenWorldLobbyRules.BrowserFilters();
            if (!string.IsNullOrEmpty(retiredCityId))
                filters.Add(new FilterOption(FilterField.StringIndex3, retiredCityId, FilterOperation.NotEqual));
            session = await MultiplayerService.Instance.MatchmakeSessionAsync(new QuickJoinOptions
            {
                CreateSession = true,
                Timeout = TimeSpan.FromSeconds(3),
                Filters = filters
            }, options, connectionCancellation.Token);
            connectionCancellation.Token.ThrowIfCancellationRequested();
            connectionCancellation.CancelAfter(Timeout.InfiniteTimeSpan);
            if (!OpenWorldLobbyRules.IsCompatible(session.Properties))
                throw new InvalidOperationException("The joined session is not a compatible social city.");
            AttachSessionEvents(session);
            disconnectedAt = -1f;
            UpdateStatus();
            SpawnTrainers();
        }
        catch
        {
            await LeaveSessionAsync();
            throw;
        }
        finally
        {
            entering = false;
            operationGate.Release();
        }
    }

    private SessionOptions BuildOptions()
    {
        var options = new SessionOptions
        {
            Type = OpenWorldLobbyRules.SessionType,
            Name = "Trainer City",
            MaxPlayers = OpenWorldLobbyRules.Capacity,
            IsPrivate = false,
            PlayerProperties = new Dictionary<string, PlayerProperty>
            {
                ["PlayerName"] = new PlayerProperty(localPlayer.Data["PlayerName"].Value, VisibilityPropertyOptions.Member),
                ["ClothingInfo"] = new PlayerProperty(localPlayer.Data["ClothingInfo"].Value, VisibilityPropertyOptions.Member)
            },
            SessionProperties = new Dictionary<string, SessionProperty>
            {
                [SessionCompatibility.PropertyName] = new SessionProperty(OpenWorldLobbyRules.Version,
                    VisibilityPropertyOptions.Public, PropertyIndex.String1),
                [OpenWorldLobbyRules.StyleProperty] = new SessionProperty(OpenWorldLobbyRules.Style,
                    VisibilityPropertyOptions.Public, PropertyIndex.String2),
                ["CityId"] = new SessionProperty(Guid.NewGuid().ToString("N"), VisibilityPropertyOptions.Public, PropertyIndex.String3)
            }
        };
        return options.WithRelayNetwork().WithNetworkOptions(new NetworkOptions
        {
#if UNITY_WEBGL
            RelayProtocol = RelayProtocol.WSS
#else
            RelayProtocol = RelayProtocol.DTLS
#endif
        }).WithHostMigration(this, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5));
    }

    private void ConfigureNetwork()
    {
        network = NetworkManager.Singleton;
        if (network == null || network.IsListening || network.ShutdownInProgress)
            throw new InvalidOperationException("The previous network session has not finished shutting down.");
        originalPlayerPrefab = network.NetworkConfig.PlayerPrefab;
        originalConnectionData = network.NetworkConfig.ConnectionData;
        originalApproval = network.NetworkConfig.ConnectionApproval;
        originalApprovalCallback = network.ConnectionApprovalCallback;
        network.NetworkConfig.PlayerPrefab = null;
        network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(localPlayer.Id);
        network.NetworkConfig.ConnectionApproval = true;
        network.ConnectionApprovalCallback = ApproveConnection;
        network.OnServerStarted += OnServerStarted;
        network.OnClientDisconnectCallback += OnDisconnected;
        OpenWorldTrainerAvatar.LocalPoseSaved += OnLocalPoseSaved;
        OpenWorldTrainerAvatar.LocalAvatarChanged += OnLocalAvatarChanged;
        configured = true;
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        var playerId = request.Payload == null ? "" : Encoding.UTF8.GetString(request.Payload);
        var valid = !leaving && request.Payload != null && request.Payload.Length <= 128 && playerId.Length > 0 && playerId != removingMember &&
            !connections.Any(pair => pair.Key != request.ClientNetworkId && pair.Value == playerId);
        response.CreatePlayerObject = false;
        response.Pending = false;
        response.Approved = false;
        if (!valid) { response.Reason = "Invalid city member."; return; }
        connections[request.ClientNetworkId] = playerId;
        var isHost = request.ClientNetworkId == NetworkManager.ServerClientId && playerId == localPlayer?.Id;
        if (isHost || (session != null && session.Players.Any(player => player.Id == playerId)))
        {
            response.Approved = true;
            return;
        }
        response.Pending = true;
        pendingApprovals[request.ClientNetworkId] = response;
        connectionDeadlines[request.ClientNetworkId] = Time.unscaledTime + 8f;
    }

    private void OnServerStarted()
    {
        if (view == null) view = FindFirstObjectByType<OpenWorldLobbyView>();
        absentMembers.Clear();
        avatars.Clear();
        connections.Clear();
        connectionDeadlines.Clear();
        pendingApprovals.Clear();
        connections[network.LocalClientId] = localPlayer.Id;
    }

    private void OnDisconnected(ulong clientId)
    {
        if (network != null && network.IsServer)
        {
            if (avatars.TryGetValue(clientId, out var avatar) && avatar != null)
                RememberPosition(connections.TryGetValue(clientId, out var id) ? id : null, avatar.transform);
            avatars.Remove(clientId);
            connections.Remove(clientId);
            connectionDeadlines.Remove(clientId);
            pendingApprovals.Remove(clientId);
        }
        if (!leaving && clientId == network.LocalClientId)
        {
            restoreLocalPose = true;
            disconnectedAt = Time.unscaledTime;
            Status = "Host disconnected. Migrating city...";
        }
    }

    private void AttachSessionEvents(ISession activeSession)
    {
        removed = () => RequestRecovery(activeSession);
        deleted = () => RequestRecovery(activeSession);
        migrationFailed = _ =>
        {
            if (session != activeSession) return;
            migrationInProgress = false;
            RequestRecovery(activeSession);
        };
        networkChanged = state =>
        {
            if (session != activeSession) return;
            if (state == NetworkState.Migrating)
            {
                absentMembers.Clear();
                migrationInProgress = true;
                restoreLocalPose = true;
            }
            if (state == NetworkState.Started) migrationInProgress = false;
            if (leaving) return;
            if (state == NetworkState.Migrating)
            {
                if (disconnectedAt < 0f) disconnectedAt = Time.unscaledTime;
                Status = "Migrating city to another trainer...";
            }
            else if (state == NetworkState.Started)
            {
                disconnectedAt = -1f;
                UpdateStatus();
            }
        };
        session.RemovedFromSession += removed;
        session.Deleted += deleted;
        session.Network.MigrationFailed += migrationFailed;
        session.Network.StateChanged += networkChanged;
    }

    private void Update()
    {
        if (leaving || entering || session == null) return;
        if (view == null) view = FindFirstObjectByType<OpenWorldLobbyView>();
        if (IsConnected) disconnectedAt = -1f;
        else if (disconnectedAt < 0f) disconnectedAt = Time.unscaledTime;
        if (disconnectedAt >= 0f && Time.unscaledTime - disconnectedAt > 90f)
            RequestRecovery(session);
        if (!IsConnected || migrationInProgress)
            absentMembers.Clear();
        if (Time.unscaledTime < nextRosterUpdate) return;
        nextRosterUpdate = Time.unscaledTime + 0.25f;
        SpawnTrainers();
        ReconcileMembers();
        if (IsConnected) UpdateStatus();
    }

    private void UpdateStatus()
    {
        Status = $"Trainer City  ·  {ChatLobby.Players.Count}/{session.MaxPlayers}" + (session.IsHost ? "  ·  Hosting" : "");
    }

    // Observe membership only while this host's network is stable. A member can
    // legitimately precede its transport connection, especially after migration.
    private void ReconcileMembers()
    {
        if (!IsConnected || !session.IsHost || !network.IsServer || migrationInProgress || leaving) return;
        var memberIds = new HashSet<string>(session.Players.Select(member => member.Id));
        foreach (var id in absentMembers.Keys.ToArray())
            if (!memberIds.Contains(id)) absentMembers.Remove(id);
        foreach (var id in memberIds)
        {
            var connected = id == localPlayer.Id || connections.Any(pair => pair.Value == id &&
                (network.ConnectedClients.ContainsKey(pair.Key) || pendingApprovals.ContainsKey(pair.Key)));
            if (!ObserveAbsentMember(id, connected, Time.unscaledTime) || removingMember != null) continue;
            RemoveAbsentMemberAsync(session, id);
            break;
        }
    }

    private bool ObserveAbsentMember(string id, bool connected, float now)
    {
        if (connected)
        {
            absentMembers.Remove(id);
            return false;
        }
        if (!absentMembers.TryGetValue(id, out var since))
        {
            absentMembers[id] = now;
            return false;
        }
        return now - since >= MemberConnectionGrace;
    }

    private async void RemoveAbsentMemberAsync(ISession observedSession, string id)
    {
        removingMember = id;
        try
        {
            await observedSession.AsHost().RemovePlayerAsync(id);
            Debug.Log("Removed a city member whose network connection did not return.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"City member cleanup will retry: {exception.Message}");
        }
        finally
        {
            if (session == observedSession && absentMembers.ContainsKey(id))
                absentMembers[id] = Time.unscaledTime - MemberConnectionGrace + MemberCleanupRetry;
            removingMember = null;
        }
    }

    private void SpawnTrainers()
    {
        if (session == null || view == null || network == null || !network.IsServer || leaving ||
            session.Network.State != NetworkState.Started) return;
        foreach (var pending in pendingApprovals.ToArray())
        {
            var isMember = connections.TryGetValue(pending.Key, out var playerId) &&
                session.Players.Any(player => player.Id == playerId);
            if (!isMember && connectionDeadlines.TryGetValue(pending.Key, out var deadline) && Time.unscaledTime < deadline) continue;
            pending.Value.Approved = isMember;
            pending.Value.Reason = isMember ? "" : "City membership could not be confirmed.";
            pending.Value.Pending = false;
            pendingApprovals.Remove(pending.Key);
            if (!isMember)
            {
                connections.Remove(pending.Key);
                connectionDeadlines.Remove(pending.Key);
            }
        }
        foreach (var clientId in network.ConnectedClientsIds.ToArray())
        {
            if (!connections.TryGetValue(clientId, out var playerId)) continue;
            var member = session.Players.FirstOrDefault(player => player.Id == playerId);
            if (member == null)
            {
                if (avatars.ContainsKey(clientId) || !connectionDeadlines.TryGetValue(clientId, out var deadline) || Time.unscaledTime > deadline)
                    network.DisconnectClient(clientId);
                continue;
            }
            if (!member.Properties.TryGetValue("PlayerName", out var name) ||
                !member.Properties.TryGetValue("ClothingInfo", out var clothes)) continue;
            if (avatars.TryGetValue(clientId, out var existing) && existing != null && existing.IsSpawned) continue;
            var position = positions.TryGetValue(playerId, out var saved) ? saved.position : view.SpawnPosition(clientId);
            var rotation = saved != null ? Quaternion.Euler(0f, saved.yaw, 0f) : Quaternion.identity;
            var avatar = Instantiate(view.AvatarPrefab, position, rotation);
            avatar.Initialize(playerId, name.Value, clothes.Value);
            avatar.NetworkObject.SpawnWithOwnership(clientId, true);
            avatars[clientId] = avatar;
            connectionDeadlines.Remove(clientId);
        }
    }

    private void RememberPosition(string playerId, Transform trainer)
    {
        if (string.IsNullOrEmpty(playerId) || trainer == null) return;
        positions[playerId] = new TrainerPosition { playerId = playerId, position = trainer.position, yaw = trainer.eulerAngles.y };
    }

    private void OnLocalPoseSaved(string playerId, Vector3 position, float yaw)
    {
        var pose = new TrainerPosition { playerId = playerId, position = position, yaw = yaw };
        if (IsActive && localPlayer != null && playerId == localPlayer.Id && IsValidPosition(pose))
            positions[playerId] = pose;
    }

    private void OnLocalAvatarChanged(OpenWorldTrainerAvatar avatar)
    {
        if (!restoreLocalPose || leaving || avatar == null || localPlayer == null || avatar.PlayerId != localPlayer.Id ||
            !OpenWorldTrainerAvatar.TryGetLocalPose(localPlayer.Id, out var position, out var yaw)) return;
        var pose = new TrainerPosition { playerId = localPlayer.Id, position = position, yaw = yaw };
        if (IsValidPosition(pose)) avatar.SetSpawnPosition(position, yaw);
        restoreLocalPose = false;
    }

    public byte[] Generate()
    {
        foreach (var entry in avatars)
            if (entry.Value != null && connections.TryGetValue(entry.Key, out var playerId))
                RememberPosition(playerId, entry.Value.transform);
        return Encoding.UTF8.GetBytes(JsonUtility.ToJson(new MigrationSnapshot
        {
            worldVersion = OpenWorldLobbyRules.Version,
            stage = OpenWorldStageJukebox.Instance != null ? OpenWorldStageJukebox.Instance.CaptureSnapshot() : null,
            cityId = session != null && session.Properties.TryGetValue("CityId", out var cityId) ? cityId.Value : null,
            trainers = positions.Values.Where(trainer => session == null || session.Players.Any(member => member.Id == trainer.playerId))
                .Take(OpenWorldLobbyRules.Capacity).ToArray()
        }));
    }

    public void Apply(byte[] migrationData)
    {
        if (migrationData == null || migrationData.Length > 65536) return;
        try
        {
            var snapshot = JsonUtility.FromJson<MigrationSnapshot>(Encoding.UTF8.GetString(migrationData));
            if (snapshot == null || snapshot.version != 1 || snapshot.trainers == null) return;
            if (snapshot.worldVersion != OpenWorldLobbyRules.Version)
            {
                positions.Clear();
                restoreLocalPose = false;
                return;
            }
            if (session != null && session.Properties.TryGetValue("CityId", out var cityId) && snapshot.cityId != cityId.Value) return;
            if (OpenWorldStageJukebox.Instance != null) OpenWorldStageJukebox.Instance.RestoreSnapshot(snapshot.stage);
            positions.Clear();
            foreach (var trainer in snapshot.trainers.Take(OpenWorldLobbyRules.Capacity))
                if (IsValidPosition(trainer)) positions[trainer.playerId] = trainer;
            if (localPlayer != null && OpenWorldTrainerAvatar.TryGetLocalPose(localPlayer.Id, out var position, out var yaw))
                OnLocalPoseSaved(localPlayer.Id, position, yaw);
        }
        catch (Exception exception) { Debug.LogWarning($"City migration snapshot rejected: {exception.Message}"); }
    }

    public static bool IsValidPosition(TrainerPosition trainer) => trainer != null &&
        !string.IsNullOrEmpty(trainer.playerId) && trainer.playerId.Length <= 128 &&
        !float.IsNaN(trainer.yaw) && !float.IsInfinity(trainer.yaw) &&
        trainer.position.sqrMagnitude < 1000000f && trainer.position.y > -30f && trainer.position.y < 100f;

    private async void RequestRecovery(ISession failedSession)
    {
        if (session != failedSession || leaving || entering || recovering || view == null) return;
        recovering = true;
        retiredCityId = failedSession.Properties.TryGetValue("CityId", out var cityId) ? cityId.Value : null;
        var previousView = view;
        Status = "City migration failed. Finding another city...";
        try { await EnterAsync(localPlayer, previousView); }
        catch (Exception exception)
        {
            Debug.LogWarning($"City recovery failed: {exception.Message}");
            Status = "City disconnected. Open the match menu to retry.";
        }
        finally { recovering = false; }
    }

    public async Task LeaveAsync()
    {
        leaving = true;
        connectionCancellation?.Cancel();
        await operationGate.WaitAsync();
        try { await LeaveSessionAsync(); }
        finally { operationGate.Release(); }
    }

    private async Task LeaveSessionAsync()
    {
        if (session != null)
        {
            var migrationDeadline = DateTime.UtcNow.AddSeconds(15);
            while (migrationInProgress)
            {
                if (DateTime.UtcNow > migrationDeadline)
                    throw new TimeoutException("City host migration is still in progress; please retry after reconnection finishes.");
                await Task.Delay(50, Application.exitCancellationToken);
            }
            session.RemovedFromSession -= removed;
            session.Deleted -= deleted;
            session.Network.MigrationFailed -= migrationFailed;
            session.Network.StateChanged -= networkChanged;
            try
            {
                if (session.IsMember && MultiplayerService.Instance.Sessions.Values.Contains(session))
                {
                    leaveTask ??= session.LeaveAsync();
                    await SessionTaskTimeout.WaitAsync(leaveTask, TimeSpan.FromSeconds(15), Application.exitCancellationToken);
                }
                var cleanupDeadline = DateTime.UtcNow.AddSeconds(15);
                while (MultiplayerService.Instance.Sessions.Values.Contains(session))
                {
                    if (DateTime.UtcNow > cleanupDeadline) throw new TimeoutException("The previous city session is still being released.");
                    await Task.Delay(50, Application.exitCancellationToken);
                }
            }
            finally
            {
                if (leaveTask != null && (leaveTask.IsFaulted || leaveTask.IsCanceled)) leaveTask = null;
            }
            var shutdownDeadline = DateTime.UtcNow.AddSeconds(5);
            while (network != null && (network.IsListening || network.ShutdownInProgress))
            {
                if (DateTime.UtcNow > shutdownDeadline) throw new TimeoutException("City network shutdown did not finish.");
                await Task.Delay(25, Application.exitCancellationToken);
            }
            session = null;
            leaveTask = null;
        }
        connectionCancellation?.Dispose();
        connectionCancellation = null;
        if (configured && network != null)
        {
            if (network.IsListening || network.ShutdownInProgress)
                throw new InvalidOperationException("The city network is still shutting down.");
            network.ConnectionApprovalCallback = originalApprovalCallback;
            network.OnServerStarted -= OnServerStarted;
            network.OnClientDisconnectCallback -= OnDisconnected;
            OpenWorldTrainerAvatar.LocalPoseSaved -= OnLocalPoseSaved;
            OpenWorldTrainerAvatar.LocalAvatarChanged -= OnLocalAvatarChanged;
            network.NetworkConfig.PlayerPrefab = originalPlayerPrefab;
            network.NetworkConfig.ConnectionData = originalConnectionData;
            network.NetworkConfig.ConnectionApproval = originalApproval;
            configured = false;
        }
        absentMembers.Clear();
        avatars.Clear();
        connections.Clear();
        connectionDeadlines.Clear();
        pendingApprovals.Clear();
        disconnectedAt = -1f;
        migrationInProgress = false;
        chatLobby = null;
        chatLocalPlayer = null;
    }

    private void OnApplicationQuit()
    {
        leaving = true;
        connectionCancellation?.Cancel();
    }

    private void OnDestroy()
    {
        leaving = true;
        connectionCancellation?.Cancel();
        OpenWorldTrainerAvatar.LocalPoseSaved -= OnLocalPoseSaved;
        OpenWorldTrainerAvatar.LocalAvatarChanged -= OnLocalAvatarChanged;
        if (network == null || !configured) return;
        network.ConnectionApprovalCallback = originalApprovalCallback;
        network.OnServerStarted -= OnServerStarted;
        network.OnClientDisconnectCallback -= OnDisconnected;
    }
}
