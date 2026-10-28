using System;
using System.Collections.Generic;
using System.Linq;
using Unite.Multiplayer;
using Unity.Netcode;
using UnityEngine;

// Lives on the existing scene NetworkObject, outside phase-specific UI roots.
// Snapshots are targeted to allies, rather than leaking both teams via a NetworkList.
public class LaneSelectionNetwork : NetworkBehaviour
{
    [SerializeField] private LanePickerUI picker;

    public static LaneSelectionNetwork Instance { get; private set; }
    public LanePickerLayout Layout { get; private set; }
    public bool IsAvailable => IsSpawned && Layout != null;
    public bool CanSelect => IsAvailable && acceptingSelections && receivedSnapshot;
    public event Action Changed;
    public event Action<Team> ServerChoicesChanged;

    private readonly Dictionary<Team, LaneSelectionRoster> serverRosters = new Dictionary<Team, LaneSelectionRoster>();
    private readonly Dictionary<ulong, uint> acknowledgedRequests = new Dictionary<ulong, uint>();
    private readonly LaneSelectionRoster localRoster = new LaneSelectionRoster();
    private LobbyController lobby;
    private NetworkManager subscribedNetworkManager;
    private bool acceptingSelections;
    private bool receivedSnapshot;
    private float nextSnapshotRequest;
    private uint requestId;
    private uint acknowledgedRequest;
    private string pendingLane = string.Empty;

    private void Awake() => Instance = this;

    public override void OnNetworkSpawn()
    {
        lobby = LobbyController.Instance;
        var map = CharactersList.Instance.GetCurrentLobbyMap();
        Layout = map != null && map.SupportsLaneSelection ? map.lanePickerLayout : null;
        acceptingSelections = true;
        localRoster.Clear();
        serverRosters.Clear();
        acknowledgedRequests.Clear();
        requestId = acknowledgedRequest = 0;
        receivedSnapshot = false;
        nextSnapshotRequest = 0;
        lobby.onLobbyUpdate += OnLobbyUpdated;
        subscribedNetworkManager = NetworkManager;
        if (IsServer) subscribedNetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        if (picker != null) picker.Initialize(this, map != null ? map.maxTeamSize : 5);
        if (IsServer && IsAvailable) RebalanceBots();
        Changed?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        Unsubscribe();
        acceptingSelections = false;
        Layout = null;
        localRoster.Clear();
        serverRosters.Clear();
        Changed?.Invoke();
    }

    public override void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    private void Unsubscribe()
    {
        if (lobby != null) lobby.onLobbyUpdate -= OnLobbyUpdated;
        if (subscribedNetworkManager != null) subscribedNetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        subscribedNetworkManager = null;
    }

    private void Update()
    {
        // Player/session identity may arrive after the scene NetworkObject spawns.
        if (!IsClient || !IsAvailable || receivedSnapshot || Time.unscaledTime < nextSnapshotRequest) return;
        nextSnapshotRequest = Time.unscaledTime + 1f;
        RequestSnapshotRpc();
    }

    public string GetLane(string playerId)
    {
        if (!IsAvailable) return string.Empty;
        if (lobby.Player.Id == playerId && requestId > acknowledgedRequest) return pendingLane;
        return localRoster.GetLane(playerId);
    }

    public IReadOnlyList<LaneSelectionRoster.Choice> Choices => localRoster.Choices;

    public void SelectLane(string laneId)
    {
        laneId = laneId ?? string.Empty;
        if (!CanSelect || (laneId.Length > 0 && Layout.FindLane(laneId) == null)) return;
        if (GetLane(lobby.Player.Id) == laneId) return;
        pendingLane = laneId;
        requestId++;
        Changed?.Invoke();
        SelectLaneRpc(laneId, requestId);
    }

    public void StopSelection()
    {
        if (IsServer && acceptingSelections && IsAvailable)
        {
            RebalanceBots();
            foreach (var roster in serverRosters.Values)
                foreach (var choice in roster.Choices)
                    if (lobby.GetPlayerByID(choice.PlayerId)?.IsBot == true)
                        lobby.BotRoster?.SaveBotLane(choice.PlayerId, choice.LaneId);
        }
        acceptingSelections = false;
        if (picker != null) picker.Close();
        Changed?.Invoke();
    }

    public void ShowPreparationLayout()
    {
        if (picker != null) picker.ShowPreparationLayout();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestSnapshotRpc(RpcParams rpcParams = default)
    {
        if (!IsAvailable) return;
        var player = GetPlayerForClient(rpcParams.Receive.SenderClientId);
        if (player == null) return;
        SendSnapshot(TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value), rpcParams.Receive.SenderClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SelectLaneRpc(string laneId, uint clientRequest, RpcParams rpcParams = default)
    {
        if (!IsAvailable || !acceptingSelections || laneId == null || (laneId.Length > 0 && Layout.FindLane(laneId) == null)) return;
        ulong sender = rpcParams.Receive.SenderClientId;
        var player = GetPlayerForClient(sender);
        if (player == null) return;
        if (acknowledgedRequests.TryGetValue(sender, out uint previous) && clientRequest <= previous) return;
        acknowledgedRequests[sender] = clientRequest;
        Team team = TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value);
        GetRoster(team).SetLane(player.Id, laneId);
        RebalanceBots(team);
        SendTeamSnapshot(team);
        ServerChoicesChanged?.Invoke(team);
    }

    public string GetServerLane(Team team, string playerId) => IsServer ? GetRoster(team).GetLane(playerId) : string.Empty;

    private void RebalanceBots()
    {
        RebalanceBots(Team.Blue);
        RebalanceBots(Team.Orange);
    }

    private void RebalanceBots(Team team)
    {
        if (!IsServer || !IsAvailable || !acceptingSelections || lobby.BotRoster?.HasBots != true ||
            BotAllocationRules.StandardLanes.Any(lane => Layout.FindLane(lane) == null)) return;
        var players = lobby.GetTeamPlayers(team);
        var roster = GetRoster(team);
        var assigned = BotAllocationRules.AllocateLanes(players.Where(player => !player.IsBot).Select(player => roster.GetLane(player.Id)),
            players.Where(player => player.IsBot).ToDictionary(player => player.Id, player => roster.GetLane(player.Id)));
        bool changed = false;
        foreach (var pair in assigned) changed |= roster.SetLane(pair.Key, pair.Value);
        if (changed) SendTeamSnapshot(team);
    }

    private GameSessionPlayer GetPlayerForClient(ulong clientId)
    {
        // Use the actual NGO-owned player object, not an ID supplied by the requester.
        var manager = lobby.PlayerNetworkManagers.Find(player => player != null && player.IsSpawned && player.OwnerClientId == clientId);
        return manager != null ? manager.LocalPlayer : null;
    }

    private LaneSelectionRoster GetRoster(Team team)
    {
        if (!serverRosters.TryGetValue(team, out var roster))
        {
            roster = new LaneSelectionRoster();
            serverRosters.Add(team, roster);
        }
        return roster;
    }

    private void SendTeamSnapshot(Team team)
    {
        foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
        {
            var player = GetPlayerForClient(clientId);
            if (player != null && player.Data["PlayerTeam"].Value == team.ToString()) SendSnapshot(team, clientId);
        }
    }

    private void SendSnapshot(Team team, ulong clientId)
    {
        var choices = GetRoster(team).Choices;
        acknowledgedRequests.TryGetValue(clientId, out uint acknowledged);
        var snapshot = new LaneSelectionSnapshot
        {
            PlayerIds = choices.Select(choice => choice.PlayerId).ToArray(),
            LaneIds = choices.Select(choice => choice.LaneId).ToArray()
        };
        ReceiveSnapshotRpc(team, snapshot, acknowledged, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    private void ReceiveSnapshotRpc(Team team, LaneSelectionSnapshot snapshot, uint acknowledged, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != Unity.Netcode.NetworkManager.ServerClientId ||
            lobby.GetLocalPlayerTeam() != team) return;
        localRoster.Replace(snapshot.PlayerIds, snapshot.LaneIds);
        acknowledgedRequest = acknowledged;
        receivedSnapshot = true;
        Changed?.Invoke();
    }

    private void OnLobbyUpdated(GameSessionView session)
    {
        if (!IsServer || !IsAvailable) return;
        foreach (var pair in serverRosters.ToArray())
        {
            var teammates = lobby.GetTeamPlayers(pair.Key);
            if (pair.Value.RemoveWhere(choice => !teammates.Any(player => player.Id == choice.PlayerId))) SendTeamSnapshot(pair.Key);
        }
        RebalanceBots();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        acknowledgedRequests.Remove(clientId);
        // Session membership may lag NGO disconnection. Prune against connected player objects too.
        var remaining = NetworkManager.ConnectedClientsIds.Where(id => id != clientId)
            .Select(GetPlayerForClient).Where(player => player != null).Select(player => player.Id).ToArray();
        foreach (var pair in serverRosters.ToArray())
            if (pair.Value.RemoveWhere(choice => !remaining.Contains(choice.PlayerId) &&
                lobby.GetPlayerByID(choice.PlayerId)?.IsBot != true)) SendTeamSnapshot(pair.Key);
    }
}

public struct LaneSelectionSnapshot : INetworkSerializable
{
    public string[] PlayerIds;
    public string[] LaneIds;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        int count = PlayerIds != null ? PlayerIds.Length : 0;
        serializer.SerializeValue(ref count);
        if (count < 0 || count > 128) throw new InvalidOperationException("Invalid lane snapshot size.");
        if (serializer.IsReader)
        {
            PlayerIds = new string[count];
            LaneIds = new string[count];
        }
        for (int i = 0; i < count; i++)
        {
            serializer.SerializeValue(ref PlayerIds[i]);
            serializer.SerializeValue(ref LaneIds[i]);
        }
    }
}
