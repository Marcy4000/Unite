using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unite.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyController : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("Debug")]
    [Tooltip("Se attivo, considera le custom games come matchmaking games.")]
    public bool treatCustomAsMatchmaking = false;
#endif

    public static LobbyController Instance { get; private set; }

    private const string LobbyNamePrefix = "PartyLobby";
    private const string SessionType = "UniteGame";
    private MainMenuUI lobbyUI;
    private ISession session;
    private ISession sessionAwaitingCleanup;
    private GameSessionView partyLobby;
    private GameSessionPlayer localPlayer;
    private string localPlayerName;
    private Task authenticationTask;
    private readonly CancellationTokenSource lifetimeCancellation = new CancellationTokenSource();
    private CancellationTokenSource matchmakingCancellation;
    private readonly SemaphoreSlim lifecycleGate = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim playerWriteGate = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim hostWriteGate = new SemaphoreSlim(1, 1);
    private readonly Dictionary<string, PlayerProperty> pendingPlayerProperties = new Dictionary<string, PlayerProperty>();
    private readonly Dictionary<string, GameSessionPlayer> gamePlayers = new Dictionary<string, GameSessionPlayer>();
    private readonly PartyMatchmakerAdapter partyMatchmaker = new PartyMatchmakerAdapter();
    private readonly List<PlayerNetworkManager> playerNetworkManagers = new List<PlayerNetworkManager>();

    private bool lobbyOperationInProgress;
    private string sessionOperationStage;
    private bool transitioningNetwork;
    private bool handlingSessionLoss;
    private bool startingGame;
    private bool loadResultsScreen;
    private bool isSearching;
    private bool isInMatchmakingGame;
    private string pendingMatchId;
    private string[] expectedMatchPlayerIds = Array.Empty<string>();
    private LobbyType currentLobbyType = LobbyType.Custom;
    private NetworkSceneManager subscribedSceneManager;
    private OriginalPartyInfo originalPartyInfo;
    private GameResults gameResults;
    private RaceGameResults raceGameResults;
    private UniteRoyaleGameResults uniteRoyaleGameResults;

    private sealed class OriginalPartyInfo
    {
        public string LeaderId;
        public string ReturnSessionId;
        public LobbyType Type;
        public bool IsPrivate;
        public Dictionary<string, SessionProperty> Properties;
    }

    public enum LobbyType { Custom, Standards }

    // These game-owned views preserve the existing UI contract without exposing Lobby SDK models.
    public GameSessionView Lobby => partyLobby;
    public GameSessionPlayer Player => localPlayer;
    public ISession Session => session;
    public List<PlayerNetworkManager> PlayerNetworkManagers => playerNetworkManagers;
    public bool ShouldLoadResultsScreen { get => loadResultsScreen; set => loadResultsScreen = value; }
    public GameResults GameResults { get => gameResults; set => gameResults = value; }
    public RaceGameResults RaceGameResults { get => raceGameResults; set => raceGameResults = value; }
    public UniteRoyaleGameResults UniteRoyaleGameResults { get => uniteRoyaleGameResults; set => uniteRoyaleGameResults = value; }
    public bool IsMatchmaking => isSearching;
    public LobbyType CurrentLobbyType => currentLobbyType;
    private bool IsPartyLeader => session != null && session.IsHost;

    public event Action<GameSessionView> onLobbyUpdate;
    public event Action<GameSessionPlayer> onPlayerDataChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        localPlayerName = $"TestPlayer {UnityEngine.Random.Range(0, 1000)}";
        lobbyUI = FindFirstObjectByType<MainMenuUI>();
        SceneManager.sceneLoaded += OnUnitySceneLoaded;
        authenticationTask = InitializeUnityAuthenticationAsync();
        ObserveTask(authenticationTask);
    }

    private void Start()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectFromServer;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        lifetimeCancellation.Cancel();
        matchmakingCancellation?.Cancel();
        UnsubscribeFromSession();
        DetachSceneEvents();
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectFromServer;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
        Instance = null;
    }

    private static async void ObserveTask(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private async Task InitializeUnityAuthenticationAsync()
    {
        LoadingScreen.Instance.ShowGenericLoadingScreen();
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var options = new InitializationOptions();
                options.SetEnvironmentName(Debug.isDebugBuild ? "development" : "production");
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
                var profile = TruncateProfile(RemoveInvalidCharacters(SystemInfo.deviceUniqueIdentifier), 30);
                options.SetProfile(string.IsNullOrEmpty(profile) ? "default" : profile);
#else
                options.SetProfile(Guid.NewGuid().ToString("N").Substring(0, 30));
#endif
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            PlayerClothesInfo clothes;
            try
            {
                clothes = PlayerClothesInfo.Deserialize(PlayerPrefs.GetString("ClothingInfo", "AAAAAAAAAAAASjMJcj8FAAAAAAJqASkBZwA="));
            }
            catch (Exception)
            {
                clothes = PlayerClothesInfo.Deserialize("AAAAAAAAAAAASjMJcj8FAAAAAAJqASkBZwA=");
            }

            localPlayer = new GameSessionPlayer(AuthenticationService.Instance.PlayerId);
            localPlayer.Data["PlayerName"] = new PlayerProperty(localPlayerName);
            localPlayer.Data["OwnerID"] = MemberPlayerProperty("0");
            localPlayer.Data["PlayerTeam"] = MemberPlayerProperty(Team.Blue.ToString());
            localPlayer.Data["PlayerPos"] = MemberPlayerProperty(NumberEncoder.ToBase64<short>(0));
            localPlayer.Data["SelectedCharacter"] = MemberPlayerProperty(NumberEncoder.ToBase64<short>(-1));
            localPlayer.Data["BattleItem"] = MemberPlayerProperty("1");
            localPlayer.Data["HeldItems"] = MemberPlayerProperty(HeldItemDatabase.SerializeHeldItems(new byte[] { 0, 0, 0 }));
            localPlayer.Data["ClothingInfo"] = new PlayerProperty(clothes.Serialize());
            localPlayer.Data["PlayerRank"] = new PlayerProperty(RankedManager.Instance != null
                ? RankedManager.Instance.GetPlayerRankSerialized() : PlayerRankData.GetDefault().Serialize());
#if UNITY_WEBGL
            NetworkManager.Singleton.GetComponent<UnityTransport>().UseWebSockets = true;
#endif
        }
        finally
        {
            LoadingScreen.Instance.HideGenericLoadingScreen();
        }
    }

    // Keep the platform authentication profile sanitation helpers.
    private string RemoveInvalidCharacters(string profile)
    {
        const string validCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";
        return new string(profile.Where(validCharacters.Contains).ToArray());
    }

    private string TruncateProfile(string profile, int maxLength)
    {
        return profile.Length > maxLength ? profile.Substring(0, maxLength) : profile;
    }

    private static PlayerProperty MemberPlayerProperty(string value) => new PlayerProperty(value, VisibilityPropertyOptions.Member);
    private static SessionProperty MemberSessionProperty(string value) => new SessionProperty(value, VisibilityPropertyOptions.Member);

    private static NetworkOptions GetNetworkOptions()
    {
        return new NetworkOptions
        {
#if UNITY_WEBGL
            RelayProtocol = RelayProtocol.WSS
#else
            RelayProtocol = RelayProtocol.DTLS
#endif
        };
    }

    private int GetRelayCapacity()
    {
        // Allocate once for all supported map sizes. The compatibility adapter can
        // subsequently shrink/grow the lobby limit without exceeding Relay capacity.
        return Math.Max(10, CharactersList.Instance.Maps.Max(map => map.maxTeamSize * map.availableTeams.Count));
    }

    private SessionOptions BuildSessionOptions(LobbyType type, Dictionary<string, SessionProperty> properties = null)
    {
        properties = properties == null ? new Dictionary<string, SessionProperty>() : new Dictionary<string, SessionProperty>(properties);
        properties["SelectedMap"] = properties.TryGetValue("SelectedMap", out var selectedMap)
            ? selectedMap : new SessionProperty(NumberEncoder.ToBase64<short>(0));
        properties["LobbyType"] = new SessionProperty(type.ToString());
        properties["HostDisplayName"] = new SessionProperty(localPlayer.Data["PlayerName"].Value);
        properties["GameVersion"] = new SessionProperty("sessions-v1", VisibilityPropertyOptions.Public, PropertyIndex.String1);
#if UNITY_EDITOR
        if (treatCustomAsMatchmaking && type == LobbyType.Custom)
            properties["IsMatchmakingLobby"] = MemberSessionProperty("true");
#endif
        return new SessionOptions
        {
            Type = SessionType,
            Name = $"{LobbyNamePrefix}_{localPlayer.Id}",
            MaxPlayers = GetRelayCapacity(),
            IsPrivate = true,
            PlayerProperties = new Dictionary<string, PlayerProperty>(localPlayer.Data),
            SessionProperties = properties
        }.WithRelayNetwork().WithNetworkOptions(GetNetworkOptions());
    }

    private JoinSessionOptions BuildJoinOptions()
    {
        return new JoinSessionOptions
        {
            Type = SessionType,
            PlayerProperties = new Dictionary<string, PlayerProperty>(localPlayer.Data)
        }.WithNetworkOptions(GetNetworkOptions());
    }

    private async Task<bool> RunLobbyOperationAsync(Func<Task> operation, bool showMainMenuOnFailure = true,
        bool showLoadingScreen = true,
        [System.Runtime.CompilerServices.CallerMemberName] string operationName = null)
    {
        if (lobbyOperationInProgress || isSearching) return false;
        lobbyOperationInProgress = true;
        await lifecycleGate.WaitAsync();
        transitioningNetwork = true;
        if (showLoadingScreen) LoadingScreen.Instance.ShowGenericLoadingScreen();
        try
        {
            sessionOperationStage = "initializing authentication";
            await authenticationTask;
            lifetimeCancellation.Token.ThrowIfCancellationRequested();
            sessionOperationStage = "executing operation";
            await operation();
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception exception)
        {
            // Message alone discards the SessionError and any preserved inner
            // exception. Include the original throw stack, not just this logger's
            // async continuation, without logging tokens or player metadata.
            var details = $"Session operation '{operationName}' failed during {sessionOperationStage}: " +
                $"{exception.GetType().FullName}: {exception}\n{exception.StackTrace}";
            if (showMainMenuOnFailure) Debug.LogError(details);
            else Debug.LogWarning(details);
            try { await LeaveCurrentSessionAsync(); }
            catch (Exception cleanupException) { Debug.LogWarning($"Session cleanup failed: {cleanupException.Message}"); }
            ResetMatchState();
            if (showMainMenuOnFailure) lobbyUI?.ShowMainMenuUI();
            return false;
        }
        finally
        {
            transitioningNetwork = false;
            lobbyOperationInProgress = false;
            sessionOperationStage = null;
            lifecycleGate.Release();
            if (showLoadingScreen) LoadingScreen.Instance.HideGenericLoadingScreen();
        }
    }

    public async void StartGame(string playerName)
    {
        try
        {
            await authenticationTask;
            await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
            await SetLocalPlayerPropertiesAsync(new Dictionary<string, PlayerProperty> { ["PlayerName"] = new PlayerProperty(playerName) });
            StartCoroutine(LoadLobbyAsync());
        }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    public async void CreateLobby(LobbyType type = LobbyType.Custom)
    {
        await RunLobbyOperationAsync(async () =>
        {
            sessionOperationStage = "leaving the previous session";
            await LeaveCurrentSessionAsync();
            sessionOperationStage = "creating the session (Lobby, Relay and NGO)";
            var created = await MultiplayerService.Instance.CreateSessionAsync(BuildSessionOptions(type), lifetimeCancellation.Token);
            sessionOperationStage = "initializing session player data";
            await ActivateSessionAsync(created);
            sessionOperationStage = "updating session capacity";
            await SessionCapacityAdapter.SetCapacityAsync(created, GetMaxPartyMembers());
            sessionOperationStage = "updating the lobby UI";
            SyncSession();
            ShowLobby();
        });
    }

    public async void TryLobbyJoin(string joinCode, bool useID = false)
    {
        await RunLobbyOperationAsync(async () =>
        {
            await LeaveCurrentSessionAsync();
            var joined = useID
                ? await MultiplayerService.Instance.JoinSessionByIdAsync(joinCode, BuildJoinOptions(), lifetimeCancellation.Token)
                : await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode, BuildJoinOptions(), lifetimeCancellation.Token);
            await ActivateSessionAsync(joined);
            CheckIfShouldChangePos(CharactersList.Instance.GetCurrentLobbyMap().maxTeamSize);
            ShowLobby();
        });
    }

    public Task<bool> QuickJoin(CancellationToken cancellationToken = default)
    {
        if (session != null) return Task.FromResult(false);
        return RunLobbyOperationAsync(async () =>
        {
            if (session != null) throw new InvalidOperationException("Leave the current session before quick joining.");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetimeCancellation.Token);
            var networkManager = NetworkManager.Singleton;
            var loadingScreenShown = false;
            void OnQuickJoinClientStarted()
            {
                // Sessions starts NGO only after finding and joining a lobby.
                // Do not cover the matchmaking bar while searching/retrying.
                if (loadingScreenShown || linked.IsCancellationRequested) return;
                loadingScreenShown = true;
                LoadingScreen.Instance.ShowGenericLoadingScreen();
            }
            networkManager.OnClientStarted += OnQuickJoinClientStarted;
            try
            {
                var joined = await MultiplayerService.Instance.MatchmakeSessionAsync(new QuickJoinOptions
                {
                    CreateSession = false,
                    Timeout = TimeSpan.FromSeconds(2),
                    Filters = GetBrowserFilters()
                }, BuildSessionOptions(LobbyType.Custom), linked.Token);
                if (linked.IsCancellationRequested)
                {
                    await joined.LeaveAsync();
                    linked.Token.ThrowIfCancellationRequested();
                }
                await ActivateSessionAsync(joined);
                if (linked.IsCancellationRequested)
                {
                    await LeaveCurrentSessionAsync();
                    linked.Token.ThrowIfCancellationRequested();
                }
                CheckIfShouldChangePos(CharactersList.Instance.GetCurrentLobbyMap().maxTeamSize);
                ShowLobby();
            }
            finally
            {
                networkManager.OnClientStarted -= OnQuickJoinClientStarted;
                if (loadingScreenShown && LoadingScreen.Instance != null)
                    LoadingScreen.Instance.HideGenericLoadingScreen();
            }
        }, showMainMenuOnFailure: false, showLoadingScreen: false);
    }

    private static List<FilterOption> GetBrowserFilters()
    {
        return new List<FilterOption>
        {
            new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater),
            new FilterOption(FilterField.IsLocked, "0", FilterOperation.Equal),
            new FilterOption(FilterField.StringIndex1, "sessions-v1", FilterOperation.Equal)
        };
    }

    public async Task<QuerySessionsResults> GetOpenLobbies()
    {
        await authenticationTask;
        return await MultiplayerService.Instance.QuerySessionsAsync(new QuerySessionsOptions
        {
            Count = 25,
            FilterOptions = GetBrowserFilters()
        });
    }

    private async Task ActivateSessionAsync(ISession activeSession)
    {
        session = activeSession;
        partyLobby = new GameSessionView();
        gamePlayers.Clear();
        session.Changed += OnSessionChanged;
        session.RemovedFromSession += OnSessionLost;
        session.Deleted += OnSessionLost;
        session.SessionHostChanged += OnSessionHostChanged;
        SyncSession();
        AttachSceneEvents();
        await SetLocalPlayerPropertiesAsync(new Dictionary<string, PlayerProperty>
        {
            ["OwnerID"] = MemberPlayerProperty(NetworkManager.Singleton.LocalClientId.ToString())
        });
        HandlePartyProperties();
    }

    private void UnsubscribeFromSession()
    {
        if (session == null) return;
        session.Changed -= OnSessionChanged;
        session.RemovedFromSession -= OnSessionLost;
        session.Deleted -= OnSessionLost;
        session.SessionHostChanged -= OnSessionHostChanged;
    }

    private void SyncSession()
    {
        if (session == null || partyLobby == null) return;
        partyLobby.Apply(session, localPlayer, pendingPlayerProperties);
        if (partyLobby.Data.TryGetValue("LobbyType", out var lobbyType) && Enum.TryParse(lobbyType.Value, out LobbyType parsed))
            currentLobbyType = parsed;
        onLobbyUpdate?.Invoke(partyLobby);
        foreach (var player in partyLobby.Players)
            onPlayerDataChanged?.Invoke(player);
    }

    private void OnSessionChanged()
    {
        SyncSession();
        HandlePartyProperties();
    }

    private void ShowLobby()
    {
        lobbyUI?.ShowLobbyUI();
        SyncSession();
        if (isSearching) lobbyUI?.ShowMatchmakingBarUI(IsPartyLeader ? CancelMatchmaking : null);
    }

    private void HandlePartyProperties()
    {
        if (session == null || isInMatchmakingGame) return;
        if (partyLobby.Data.TryGetValue("MatchmakingStatus", out var status))
        {
            var searching = status.Value == "searching";
            if (searching != isSearching)
            {
                isSearching = searching;
                if (searching)
                {
                    CaptureOriginalParty();
                    lobbyUI?.ShowMatchmakingBarUI(IsPartyLeader ? CancelMatchmaking : null);
                }
                else lobbyUI?.HideMatchmakingBarUI();
            }
        }
        if (!IsPartyLeader && partyLobby.Data.TryGetValue("RequestedTeam", out var requestedTeam) && IsStandardsLobby())
        {
            if (localPlayer.Data["PlayerTeam"].Value != requestedTeam.Value)
                ObserveTask(SetLocalPlayerPropertiesAsync(new Dictionary<string, PlayerProperty>
                {
                    ["PlayerTeam"] = MemberPlayerProperty(requestedTeam.Value)
                }));
        }
        if (!IsPartyLeader && partyLobby.Data.TryGetValue("PendingMatchId", out var match) &&
            !string.IsNullOrEmpty(match.Value) && pendingMatchId != match.Value)
        {
            pendingMatchId = match.Value;
            CaptureOriginalParty();
            isSearching = false;
            ObserveTask(TransitionToMatchAsync(match.Value));
        }
    }

    private async Task SetLocalPlayerPropertiesAsync(Dictionary<string, PlayerProperty> changes)
    {
        if (localPlayer == null) return;
        var targetSession = session;
        foreach (var change in changes)
        {
            localPlayer.Data[change.Key] = change.Value;
            if (targetSession != null) pendingPlayerProperties[change.Key] = change.Value;
        }
        // Local properties can be edited before a session exists (name, clothes, held items).
        if (targetSession == null) return;
        SyncSession();
        await playerWriteGate.WaitAsync(lifetimeCancellation.Token);
        try
        {
            if (session != targetSession || !targetSession.IsMember) return;
            targetSession.CurrentPlayer.SetProperties(changes);
            await SaveSessionChangesAsync(targetSession.SaveCurrentPlayerDataAsync);
        }
        finally
        {
            foreach (var change in changes)
                if (pendingPlayerProperties.TryGetValue(change.Key, out var pending) && ReferenceEquals(pending, change.Value))
                    pendingPlayerProperties.Remove(change.Key);
            playerWriteGate.Release();
            if (session == targetSession) SyncSession();
        }
    }

    private async Task UpdateHostAsync(Action<IHostSession> update, int? capacity = null)
    {
        var targetSession = session;
        if (targetSession == null || !targetSession.IsHost) return;
        await hostWriteGate.WaitAsync(lifetimeCancellation.Token);
        try
        {
            if (session != targetSession || !targetSession.IsMember) return;
            if (capacity.HasValue)
            {
                if (capacity.Value > GetRelayCapacity()) throw new InvalidOperationException("Requested capacity exceeds the Relay allocation.");
                await SessionCapacityAdapter.SetCapacityAsync(targetSession, capacity.Value);
            }
            var host = targetSession.AsHost();
            update(host);
            await SaveSessionChangesAsync(host.SavePropertiesAsync);
            SyncSession();
        }
        finally { hostWriteGate.Release(); }
    }

    private async Task SaveSessionChangesAsync(Func<Task> save)
    {
        for (var attempt = 0; ; attempt++)
        {
            lifetimeCancellation.Token.ThrowIfCancellationRequested();
            try
            {
                await save();
                return;
            }
            catch (SessionException exception) when (exception.Error == SessionError.RateLimitExceeded && attempt < 2)
            {
                await Task.Delay(1100, lifetimeCancellation.Token);
            }
        }
    }

    private void SetPlayerProperty(string key, string value, VisibilityPropertyOptions visibility = VisibilityPropertyOptions.Member)
    {
        if (localPlayer == null) return;
        if (localPlayer.Data.TryGetValue(key, out var previous) && previous.Value == value) return;
        ObserveTask(SetLocalPlayerPropertiesAsync(new Dictionary<string, PlayerProperty> { [key] = new PlayerProperty(value, visibility) }));
    }

    public void ChangePlayerName(string newName)
    {
        SetPlayerProperty("PlayerName", newName, VisibilityPropertyOptions.Public);
        if (IsPartyLeader) ObserveTask(UpdateHostAsync(host => host.SetProperty("HostDisplayName", new SessionProperty(newName))));
    }
    public void UpdatePlayerClothes(PlayerClothesInfo clothesInfo) => SetPlayerProperty("ClothingInfo", clothesInfo.Serialize(), VisibilityPropertyOptions.Public);
    public void UpdatePlayerHeldItems(string heldItems) => SetPlayerProperty("HeldItems", heldItems);
    public void ChangePlayerCharacter(short characterID) => SetPlayerProperty("SelectedCharacter", NumberEncoder.ToBase64(characterID));
    public void ChangePlayerBattleItem(string battleItemID) => SetPlayerProperty("BattleItem", battleItemID);
    public void UpdatePlayerRank()
    {
        if (RankedManager.Instance != null)
            SetPlayerProperty("PlayerRank", RankedManager.Instance.GetPlayerRankSerialized(), VisibilityPropertyOptions.Public);
    }

    public void UpdatePlayerItemsPokemonAndBattleItem(string heldItems, short characterID, string battleItemID)
    {
        ObserveTask(SetLocalPlayerPropertiesAsync(new Dictionary<string, PlayerProperty>
        {
            ["HeldItems"] = MemberPlayerProperty(heldItems),
            ["SelectedCharacter"] = MemberPlayerProperty(NumberEncoder.ToBase64(characterID)),
            ["BattleItem"] = MemberPlayerProperty(battleItemID)
        }));
    }

    public void PlayerSwitchTeam()
    {
        UpdatePlayerTeamAndPos(GetLocalPlayerTeam() == Team.Blue ? Team.Orange : Team.Blue,
            NumberEncoder.FromBase64<short>(localPlayer.Data["PlayerPos"].Value));
    }

    public void UpdatePlayerTeamAndPos(Team team, short pos)
    {
        if (localPlayer.Data["PlayerTeam"].Value == team.ToString() &&
            localPlayer.Data["PlayerPos"].Value == NumberEncoder.ToBase64(pos)) return;
        ObserveTask(SetLocalPlayerPropertiesAsync(new Dictionary<string, PlayerProperty>
        {
            ["PlayerTeam"] = MemberPlayerProperty(team.ToString()),
            ["PlayerPos"] = MemberPlayerProperty(NumberEncoder.ToBase64(pos))
        }));
    }

    public void CheckIfShouldChangePos(int maxTeamSize)
    {
        if (partyLobby == null || maxTeamSize <= 0) return;
        var teams = CharactersList.Instance.GetCurrentLobbyMap().availableTeams;
        var team = GetLocalPlayerTeam();
        var position = NumberEncoder.FromBase64<short>(localPlayer.Data["PlayerPos"].Value);
        bool Occupied(Team candidateTeam, short candidatePosition) => partyLobby.Players.Any(player =>
            player.Id != localPlayer.Id && player.Data["PlayerTeam"].Value == candidateTeam.ToString() &&
            NumberEncoder.FromBase64<short>(player.Data["PlayerPos"].Value) == candidatePosition);
        if (teams.Contains(team) && position >= 0 && position < maxTeamSize && !Occupied(team, position)) return;
        foreach (var candidateTeam in teams)
            for (short candidatePosition = 0; candidatePosition < maxTeamSize; candidatePosition++)
                if (!Occupied(candidateTeam, candidatePosition))
                {
                    UpdatePlayerTeamAndPos(candidateTeam, candidatePosition);
                    return;
                }
    }

    public void ChangeLobbyVisibility(bool isPrivate) => ObserveTask(UpdateHostAsync(host => host.IsPrivate = isPrivate));
    public void SetLobbyLocked(bool isLocked) => ObserveTask(UpdateHostAsync(host => host.IsLocked = isLocked));
    public void SetLobbyLockedAndPrivate(bool isLocked) => ObserveTask(UpdateHostAsync(host =>
    {
        host.IsLocked = isLocked;
        host.IsPrivate = isLocked;
    }));

    public void ChangeLobbyMap(MapInfo map)
    {
        if (!IsPartyLeader || isSearching) return;
        var capacity = currentLobbyType == LobbyType.Standards ? map.maxTeamSize : map.maxTeamSize * map.availableTeams.Count;
        ObserveTask(UpdateHostAsync(host => host.SetProperty("SelectedMap", new SessionProperty(
            NumberEncoder.ToBase64(CharactersList.Instance.GetMapID(map)))), capacity));
        // Each member fixes its own team/slot on the resulting session event;
        // this avoids unauthorized writes to another player's properties.
    }

    public void ChangeLobbyType(LobbyType newType)
    {
        if (!IsPartyLeader || isSearching || currentLobbyType == newType) return;
        var map = CharactersList.Instance.GetCurrentLobbyMap();
        var capacity = newType == LobbyType.Standards ? map.maxTeamSize : map.maxTeamSize * map.availableTeams.Count;
        ObserveTask(UpdateHostAsync(host =>
        {
            host.SetProperty("LobbyType", new SessionProperty(newType.ToString()));
            host.SetProperty("MatchmakingStatus", null);
            host.SetProperty("RequestedTeam", null);
            host.SetProperty("PendingMatchId", null);
        }, capacity));
    }

    public bool IsCustomLobby() => currentLobbyType == LobbyType.Custom;
    public bool IsStandardsLobby() => currentLobbyType == LobbyType.Standards;
    public int GetMaxPartyMembers()
    {
        var map = CharactersList.Instance.GetCurrentLobbyMap();
        return IsStandardsLobby() ? map.maxTeamSize : map.maxTeamSize * map.availableTeams.Count;
    }

    public async void KickPlayer(string playerID)
    {
        if (!IsPartyLeader || playerID == localPlayer.Id) return;
        try { await session.AsHost().RemovePlayerAsync(playerID); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private void CaptureOriginalParty()
    {
        if (partyLobby == null || isInMatchmakingGame) return;
        originalPartyInfo = new OriginalPartyInfo
        {
            LeaderId = partyLobby.HostId,
            ReturnSessionId = partyLobby.Data.TryGetValue("ReturnPartyId", out var returnId) ? returnId.Value : null,
            Type = currentLobbyType,
            IsPrivate = partyLobby.IsPrivate,
            Properties = new Dictionary<string, SessionProperty>(partyLobby.Data.Where(property =>
                property.Key == "SelectedMap" || property.Key == "LobbyType"))
        };
    }

    public List<Unity.Services.Matchmaker.Models.Player> GetMatchmakerPlayersWithTeam()
    {
        return partyLobby.Players.Select(player => new Unity.Services.Matchmaker.Models.Player(player.Id,
            new Dictionary<string, string> { ["Team"] = player.Data["PlayerTeam"].Value })).ToList();
    }

    public async void FindMatch()
    {
        if (!IsPartyLeader || isSearching || lobbyOperationInProgress) return;
        isSearching = true;
        var sourceSession = session;
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeCancellation.Token);
        matchmakingCancellation = cancellation;
        try
        {
            var assignedTeam = UnityEngine.Random.value < 0.5f ? Team.Blue : Team.Orange;
            await SetLocalPlayerPropertiesAsync(new Dictionary<string, PlayerProperty>
            {
                ["PlayerTeam"] = MemberPlayerProperty(assignedTeam.ToString())
            });
            await UpdateHostAsync(host =>
            {
                host.SetProperty("ReturnPartyId", MemberSessionProperty(Guid.NewGuid().ToString()));
                host.SetProperty("PendingMatchId", null);
                host.SetProperty("RequestedTeam", MemberSessionProperty(assignedTeam.ToString()));
                host.SetProperty("MatchmakingStatus", MemberSessionProperty("searching"));
                host.IsLocked = true;
            });
            CaptureOriginalParty();
            lobbyUI?.ShowMatchmakingBarUI(CancelMatchmaking);
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (partyLobby.Players.Any(player => player.Data["PlayerTeam"].Value != assignedTeam.ToString()))
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Party members did not confirm their teams.");
                await Task.Delay(100, cancellation.Token);
            }
            var matchId = await partyMatchmaker.FindMatchAsync(GetMatchmakerPlayersWithTeam(), "StandardsQueue", cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (session != sourceSession) return;
            pendingMatchId = matchId;
            await UpdateHostAsync(host => host.SetProperty("PendingMatchId", MemberSessionProperty(matchId)));
            isSearching = false;
            await TransitionToMatchAsync(matchId);
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            Debug.LogError($"Party matchmaking failed: {exception.Message}");
            if (session == sourceSession) await StopPartySearchAsync();
        }
        finally
        {
            if (matchmakingCancellation == cancellation)
            {
                matchmakingCancellation = null;
                if (session == sourceSession) await StopPartySearchAsync();
            }
            cancellation.Dispose();
        }
    }

    private async Task StopPartySearchAsync()
    {
        isSearching = false;
        try
        {
            await UpdateHostAsync(host =>
            {
                host.SetProperty("MatchmakingStatus", MemberSessionProperty("stopped"));
                host.SetProperty("RequestedTeam", null);
                host.SetProperty("PendingMatchId", null);
                host.IsLocked = false;
            });
        }
        catch (Exception exception) { Debug.LogWarning($"Could not reset party search: {exception.Message}"); }
        lobbyUI?.HideMatchmakingBarUI();
    }

    public async void CancelMatchmaking()
    {
        if (!IsPartyLeader || matchmakingCancellation == null) return;
        matchmakingCancellation.Cancel();
        // FindMatch owns ticket cleanup. Keep searching locked until its finally
        // block completes so a new search cannot be cancelled by the old one.
        await Task.Yield();
    }

    private async Task TransitionToMatchAsync(string matchId)
    {
        await RunLobbyOperationAsync(async () =>
        {
            CaptureOriginalParty();
            isInMatchmakingGame = true;
            isSearching = false;
            var results = await partyMatchmaker.GetResultsAsync(matchId, lifetimeCancellation.Token);
            var roster = new MatchRoster(results.MatchProperties, CharactersList.Instance.GetMapFromID(0).maxTeamSize);
            expectedMatchPlayerIds = roster.Players.Keys.ToArray();
            if (!roster.Players.TryGetValue(localPlayer.Id, out var assignment))
                throw new InvalidOperationException("The local player is not in the assigned match roster.");

            // Acknowledge the handoff before leaving. The party host keeps Relay
            // alive until every remaining member has received the match ID.
            await SetLocalPlayerPropertiesAsync(new Dictionary<string, PlayerProperty>
            {
                ["MatchReady"] = MemberPlayerProperty(matchId)
            });
            if (IsPartyLeader)
            {
                var handoffDeadline = DateTime.UtcNow.AddSeconds(30);
                while (partyLobby.Players.Any(player => player.Id != localPlayer.Id &&
                    (!player.Data.TryGetValue("MatchReady", out var ready) || ready.Value != matchId)))
                {
                    if (DateTime.UtcNow > handoffDeadline) throw new TimeoutException("Party members did not acknowledge the match handoff.");
                    await Task.Delay(100, lifetimeCancellation.Token);
                }
            }

            await LeaveCurrentSessionAsync();
            localPlayer.Data.Remove("MatchReady");
            await SetLocalPlayerPropertiesAsync(new Dictionary<string, PlayerProperty>
            {
                ["PlayerTeam"] = MemberPlayerProperty(assignment.Team),
                ["PlayerPos"] = MemberPlayerProperty(NumberEncoder.ToBase64(assignment.Position))
            });
            var options = BuildSessionOptions(LobbyType.Custom);
            options.MaxPlayers = Math.Max(GetRelayCapacity(), expectedMatchPlayerIds.Length);
            options.Name = $"MATCH_{matchId}";
            options.SessionProperties["IsMatchmakingLobby"] = MemberSessionProperty("true");
            var matched = await MultiplayerService.Instance.CreateOrJoinSessionAsync(matchId, options, lifetimeCancellation.Token);
            await ActivateSessionAsync(matched);
            lobbyUI?.HideMatchmakingBarUI();
            // Every client must have the complete session roster before the host
            // changes scenes: character selection builds its UI from that roster.
            var rosterDeadline = DateTime.UtcNow.AddSeconds(45);
            while (expectedMatchPlayerIds.Any(playerId => !matched.HasPlayer(playerId)))
            {
                if (session != matched) throw new OperationCanceledException();
                if (DateTime.UtcNow > rosterDeadline) throw new TimeoutException("The assigned match roster did not arrive.");
                await Task.Delay(100, lifetimeCancellation.Token);
            }
            await SetLocalPlayerPropertiesAsync(new Dictionary<string, PlayerProperty>
            {
                ["MatchReady"] = MemberPlayerProperty(matchId)
            });
            if (matched.IsHost)
            {
                var deadline = DateTime.UtcNow.AddSeconds(45);
                while (!ValidateMatchmakingLobbyReady())
                {
                    lifetimeCancellation.Token.ThrowIfCancellationRequested();
                    if (session != matched) throw new OperationCanceledException();
                    if (DateTime.UtcNow > deadline) throw new TimeoutException("Not all assigned players connected to the match.");
                    await Task.Delay(100, lifetimeCancellation.Token);
                }
                StartGame();
            }
        });
    }

    private bool ValidateMatchmakingLobbyReady()
    {
        if (session == null || !NetworkManager.Singleton.IsHost) return false;
        var owners = new HashSet<ulong>();
        foreach (var playerId in expectedMatchPlayerIds)
        {
            var player = partyLobby.Players.Find(member => member.Id == playerId);
            if (player == null || !player.Data.TryGetValue("MatchReady", out var ready) || ready.Value != pendingMatchId ||
                !player.Data.TryGetValue("OwnerID", out var owner) ||
                !ulong.TryParse(owner.Value, out var ownerId) || !owners.Add(ownerId) ||
                !NetworkManager.Singleton.ConnectedClientsIds.Contains(ownerId)) return false;
        }
        return expectedMatchPlayerIds.Length > 0;
    }

    private async Task LeaveCurrentSessionAsync()
    {
        var leaving = session ?? sessionAwaitingCleanup;
        sessionAwaitingCleanup = leaving;
        UnsubscribeFromSession();
        DetachSceneEvents();
        // Drain writes while the session handle is still valid, then give its
        // network module sole responsibility for shutting NGO down.
        await playerWriteGate.WaitAsync();
        await hostWriteGate.WaitAsync();
        try
        {
            session = null;
            pendingPlayerProperties.Clear();
            if (leaving != null)
            {
                if (MultiplayerService.Instance.Sessions.Values.Contains(leaving) && leaving.IsMember)
                    await leaving.LeaveAsync();
                // Kicks/deletions are cleaned up asynchronously by MPS. Do not
                // reuse a session type or NetworkManager until that has finished.
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (MultiplayerService.Instance.Sessions.Values.Contains(leaving))
                {
                    if (DateTime.UtcNow > deadline) throw new TimeoutException("The previous session has not finished shutting down.");
                    await Task.Delay(100, lifetimeCancellation.Token);
                }
            }
            sessionAwaitingCleanup = null;
        }
        finally
        {
            playerWriteGate.Release();
            hostWriteGate.Release();
            partyLobby = null;
            playerNetworkManagers.Clear();
            PlayerClothesPreloader.Instance?.ClearAllModels();
        }
    }

    public async void LeaveLobby()
    {
        matchmakingCancellation?.Cancel();
        isSearching = false;
        await RunLobbyOperationAsync(async () =>
        {
            await LeaveCurrentSessionAsync();
            ResetMatchState();
            lobbyUI?.ShowMainMenuUI();
        });
    }

    private void ResetMatchState()
    {
        isSearching = false;
        isInMatchmakingGame = false;
        pendingMatchId = null;
        expectedMatchPlayerIds = Array.Empty<string>();
        localPlayer?.Data.Remove("MatchReady");
        originalPartyInfo = null;
        gamePlayers.Clear();
    }

    private void OnSessionLost() => ObserveTask(HandleSessionLossAsync());
    private void OnSessionHostChanged(string hostId)
    {
        // Session ownership migration is not gameplay state migration. Until the
        // game implements the latter, do not resume a match on a new NGO host.
        if (!transitioningNetwork) OnSessionLost();
    }

    private async Task HandleSessionLossAsync()
    {
        if (transitioningNetwork || handlingSessionLoss || session == null) return;
        handlingSessionLoss = true;
        matchmakingCancellation?.Cancel();
        isSearching = false;
        try
        {
            await RunLobbyOperationAsync(async () =>
            {
                await LeaveCurrentSessionAsync();
                ResetMatchState();
                if (SceneManager.GetActiveScene().name != "LobbyScene")
                    await SceneManager.LoadSceneAsync("LobbyScene", LoadSceneMode.Single);
                lobbyUI?.ShowMainMenuUI();
            });
        }
        finally { handlingSessionLoss = false; }
    }

    private void OnClientDisconnectFromServer(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId &&
            !NetworkManager.Singleton.IsHost && !transitioningNetwork) OnSessionLost();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;
        AttachSceneEvents();
        if (session != null) SetPlayerProperty("OwnerID", clientId.ToString());
    }

    private void AttachSceneEvents()
    {
        var sceneManager = NetworkManager.Singleton.SceneManager;
        if (sceneManager == null || subscribedSceneManager == sceneManager) return;
        DetachSceneEvents();
        subscribedSceneManager = sceneManager;
        subscribedSceneManager.OnSceneEvent += LoadingScreen.Instance.OnSceneEvent;
        subscribedSceneManager.OnSceneEvent += OnSceneEvent;
    }

    private void DetachSceneEvents()
    {
        if (subscribedSceneManager == null) return;
        if (LoadingScreen.Instance != null) subscribedSceneManager.OnSceneEvent -= LoadingScreen.Instance.OnSceneEvent;
        subscribedSceneManager.OnSceneEvent -= OnSceneEvent;
        subscribedSceneManager = null;
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "LobbyScene") lobbyUI = FindFirstObjectByType<MainMenuUI>();
        if (scene.name == "CharacterSelect" || scene.name == "DraftSelect" || scene.name == "RacingReadyScreen")
        {
            gamePlayers.Clear();
            if (partyLobby != null)
                foreach (var player in partyLobby.Players) gamePlayers[player.Id] = player;
        }
    }

    public async void StartGame()
    {
        if (!IsPartyLeader || startingGame) return;
        if (IsStandardsLobby()) { FindMatch(); return; }
        startingGame = true;
        var targetSession = session;
        try
        {
            await UpdateHostAsync(host => { host.IsLocked = true; host.IsPrivate = true; });
            if (session != targetSession || !session.IsHost) return;
            var mapMode = CharactersList.Instance.GetCurrentLobbyMap().characterSelectType switch
            {
                CharacterSelectType.Draft => "DraftSelect",
                CharacterSelectType.PsyduckRacing => "RacingReadyScreen",
                _ => "CharacterSelect"
            };
            NetworkManager.Singleton.SceneManager.LoadScene(mapMode, LoadSceneMode.Single);
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally { startingGame = false; }
    }

    public void LoadGameMap()
    {
        if (!IsPartyLeader) return;
        NetworkManager.Singleton.SceneManager.LoadScene(CharactersList.Instance.GetCurrentLobbyMap().sceneName, LoadSceneMode.Single);
    }

    public async void LoadResultsScreen()
    {
        if (!IsPartyLeader) return;
        try
        {
            await UpdateHostAsync(host => host.IsLocked = false);
            if (session == null) return;
            LoadingScreen.Instance.ShowGenericLoadingScreen();
            NetworkManager.Singleton.SceneManager.LoadScene("LobbyScene", LoadSceneMode.Single);
        }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    private IEnumerator LoadLobbyAsync()
    {
        LoadingScreen.Instance.ShowGenericLoadingScreen();
        yield return SceneManager.LoadSceneAsync("LobbyScene", LoadSceneMode.Single);
        LoadingScreen.Instance.HideGenericLoadingScreen();
    }

    private IEnumerator LoadResultsScreenAsync()
    {
        yield return SceneManager.LoadSceneAsync(CharactersList.Instance.GetCurrentLobbyMap().resultScreenScene, LoadSceneMode.Additive);
        lobbyUI.DisableLobbyScene();
    }

    public async void ReturnToLobby(bool leaveLobby)
    {
        if (!leaveLobby) { StartCoroutine(LoadLobbyAsync()); return; }
        matchmakingCancellation?.Cancel();
        isSearching = false;
        await RunLobbyOperationAsync(async () =>
        {
            await LeaveCurrentSessionAsync();
            ResetMatchState();
            await SceneManager.LoadSceneAsync("LobbyScene", LoadSceneMode.Single);
            lobbyUI?.ShowMainMenuUI();
        });
    }

    public void ReturnToHomeWithoutLeavingLobby() => StartCoroutine(ReturnToHomeWithoutLeavingLobbyAsync());

    private IEnumerator ReturnToHomeWithoutLeavingLobbyAsync()
    {
        LoadingScreen.Instance.ShowGenericLoadingScreen();
        yield return SceneManager.UnloadSceneAsync(CharactersList.Instance.GetCurrentLobbyMap().resultScreenScene);
        lobbyUI.EnableLobbyScene();
        if (isInMatchmakingGame && originalPartyInfo != null)
        {
            ReturnToOriginalLobbyAfterMatch();
            yield break;
        }
        lobbyUI.ShowLobbyUI();
        LoadingScreen.Instance.HideGenericLoadingScreen();
    }

    public async void ReturnToOriginalLobbyAfterMatch()
    {
        if (!isInMatchmakingGame || originalPartyInfo == null) return;
        var original = originalPartyInfo;
        await RunLobbyOperationAsync(async () =>
        {
            await LeaveCurrentSessionAsync();
            ResetMatchState();
            if (string.IsNullOrEmpty(original.ReturnSessionId))
            {
                lobbyUI?.ShowMainMenuUI();
                return;
            }
            ISession restored;
            if (original.LeaderId == localPlayer.Id)
            {
                var options = BuildSessionOptions(original.Type, original.Properties);
                restored = await MultiplayerService.Instance.CreateOrJoinSessionAsync(original.ReturnSessionId, options, lifetimeCancellation.Token);
            }
            else
            {
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (true)
                {
                    try
                    {
                        restored = await MultiplayerService.Instance.JoinSessionByIdAsync(original.ReturnSessionId, BuildJoinOptions(), lifetimeCancellation.Token);
                        break;
                    }
                    catch (SessionException exception) when (exception.Error == SessionError.SessionNotFound && DateTime.UtcNow < deadline)
                    {
                        await Task.Delay(1100, lifetimeCancellation.Token);
                    }
                }
            }
            await ActivateSessionAsync(restored);
            if (restored.IsHost)
                await UpdateHostAsync(host => host.IsPrivate = original.IsPrivate, GetMaxPartyMembers());
            CheckIfShouldChangePos(CharactersList.Instance.GetCurrentLobbyMap().maxTeamSize);
            ShowLobby();
        });
    }

    public void OnMatchmakingGameEnded() => ReturnToOriginalLobbyAfterMatch();

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType == SceneEventType.LoadComplete && sceneEvent.SceneName == "LobbyScene" && loadResultsScreen)
        {
            StartCoroutine(LoadResultsScreenAsync());
            loadResultsScreen = false;
        }
    }

    public GameSessionPlayer GetPlayerByID(string playerID)
    {
        return partyLobby?.Players.Find(player => player.Id == playerID) ??
            (gamePlayers.TryGetValue(playerID, out var saved) ? saved : localPlayer?.Id == playerID ? localPlayer : null);
    }

    public GameSessionPlayer[] GetTeamPlayers(Team team)
    {
        var players = partyLobby?.Players ?? new List<GameSessionPlayer>();
        return players.Where(player => player.Data.TryGetValue("PlayerTeam", out var property) && property.Value == team.ToString())
            .OrderBy(player => NumberEncoder.FromBase64<short>(player.Data["PlayerPos"].Value)).ToArray();
    }

    public Team GetLocalPlayerTeam() => TeamMember.GetTeamFromString(localPlayer.Data["PlayerTeam"].Value);
    public bool IsPlayerInResultScreen(GameSessionPlayer player) => playerNetworkManagers.Any(manager => manager.LocalPlayer?.Id == player.Id && manager.IsInResultScreen);
    public bool IsAnyPlayerInResultScreen() => playerNetworkManagers.Any(manager => manager.IsInResultScreen);
}
