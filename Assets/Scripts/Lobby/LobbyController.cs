using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Matchmaker;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using JSAM;

public class LobbyController : MonoBehaviour
{
    public static LobbyController Instance { get; private set; }

    private MainMenuUI lobbyUI;
    private const string LobbyNamePrefix = "PartyLobby";
    private int maxPartyMembers = 10;
    private Player localPlayer;
    private Lobby partyLobby;

    private float lobbyHeartBeatTimer = 15.0f;
    private float lobbyUpdateTimer = 1.1f;

    private string localPlayerName;

    private ILobbyEvents lobbyEvents;

    private GameResults gameResults;
    private RaceGameResults raceGameResults;

    private List<PlayerNetworkManager> playerNetworkManagers = new List<PlayerNetworkManager>();
    private bool loadResultsScreen = false;

    private bool isSearching = false;
    private Unity.Services.Matchmaker.Models.CreateTicketResponse createTicketResponse;

#if UNITY_WEBGL
    private string connectionType = "wss";
#else
    private string connectionType = "dtls";
#endif

    private Lobby originalPartyLobby;
    private string originalLobbyId;
    private string originalLobbyCode;
    private bool isInMatchmakingGame = false;
    private bool isPartyLeader => partyLobby != null && partyLobby.HostId == localPlayer.Id;
    private string partyMatchmakerTicketId = null;

    public Lobby Lobby => partyLobby;
    public Player Player => localPlayer;
    public List<PlayerNetworkManager> PlayerNetworkManagers => playerNetworkManagers;

    public bool ShouldLoadResultsScreen { get => loadResultsScreen; set => loadResultsScreen = value; }

    public GameResults GameResults { get => gameResults; set => gameResults = value;}
    public RaceGameResults RaceGameResults { get => raceGameResults; set => raceGameResults = value; }

    public ILobbyEvents LobbyEvents => lobbyEvents;

    public event Action<Lobby> onLobbyUpdate;

    public enum LobbyType
    {
        Custom,
        Standards
    }

    private LobbyType currentLobbyType = LobbyType.Custom;
    public LobbyType CurrentLobbyType => currentLobbyType;

    private void Awake()
    {
        if (Instance != null && this != Instance)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        localPlayerName = $"TestPlayer {UnityEngine.Random.Range(0, 1000)}";
        lobbyUI = FindObjectOfType<MainMenuUI>();

        InitializeUnityAuthentication();

        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == "LobbyScene")
            {
                lobbyUI = FindObjectOfType<MainMenuUI>();
            }
        };
    }

    private void Start()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectFromServer;

        NetworkManager.Singleton.OnClientConnectedCallback += (clientId) =>
        {
            if (clientId != NetworkManager.Singleton.LocalClientId)
            {
                return;
            }
            StartCoroutine(UpdatePlayerOwnerID(clientId));
        };
    }

    public async void StartGame(string playerName)
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            Debug.LogError("Unity Services not initialized.");
            return;
        }

        LoadingScreen.Instance.ShowGenericLoadingScreen();

        await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);

        ChangePlayerName(playerName);
        StartCoroutine(LoadLobbyAsync());
    }

    private IEnumerator LoadLobbyAsync()
    {
        LoadingScreen.Instance.ShowGenericLoadingScreen();
        var task = SceneManager.LoadSceneAsync("LobbyScene", LoadSceneMode.Single);
        while (!task.isDone)
        {
            yield return null;
        }
        LoadingScreen.Instance.HideGenericLoadingScreen();
    }

    private void Update()
    {
        HandleLobbyHeartBeat();

        HandleLobbyPollForUpdates();
    }

    private async void InitializeUnityAuthentication()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            LoadingScreen.Instance.ShowGenericLoadingScreen();
            InitializationOptions options = new InitializationOptions();

            string eviromentName = Debug.isDebugBuild ? "development" : "production";
            options.SetEnvironmentName(eviromentName);

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            string profile = SystemInfo.deviceUniqueIdentifier;
            profile = RemoveInvalidCharacters(profile);
            profile = TruncateProfile(profile, 30);
            options.SetProfile(profile);
#else
            options.SetProfile(UnityEngine.Random.Range(0, 1000).ToString());
#endif

            await UnityServices.InitializeAsync(options);

            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            PlayerClothesInfo clothes;

            try
            {
                clothes = PlayerPrefs.HasKey("ClothingInfo") ? PlayerClothesInfo.Deserialize(PlayerPrefs.GetString("ClothingInfo")) : PlayerClothesInfo.Deserialize("AAAAAAAAAAAASjMJcj8FAAAAAAJqASkBZwA=");
            }
            catch (Exception)
            {
                clothes = PlayerClothesInfo.Deserialize("AAAAAAAAAAAASjMJcj8FAAAAAAJqASkBZwA=");
            }

            localPlayer = new Player(AuthenticationService.Instance.PlayerId, AuthenticationService.Instance.Profile, new Dictionary<string, PlayerDataObject>
            {
                {"PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, localPlayerName)},
                {"OwnerID", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "0")},
                {"PlayerTeam", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "Blue")},
                {"PlayerPos", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, NumberEncoder.ToBase64<short>(0))},
                {"SelectedCharacter", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, NumberEncoder.ToBase64<short>(-1))},
                {"BattleItem", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "1")},
                {"HeldItems", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, HeldItemDatabase.SerializeHeldItems(new byte[] {0, 0, 0}))},
                {"ClothingInfo", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, clothes.Serialize())}
            });

#if UNITY_WEBGL
            NetworkManager.Singleton.GetComponent<UnityTransport>().UseWebSockets = true;
#endif

            LoadingScreen.Instance.HideGenericLoadingScreen();
        }
    }

    private string RemoveInvalidCharacters(string profile)
    {
        string validCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";
        return new string(profile.Where(c => validCharacters.Contains(c)).ToArray());
    }

    private string TruncateProfile(string profile, int maxLength)
    {
        if (profile.Length > maxLength)
        {
            return profile.Substring(0, maxLength);
        }
        return profile;
    }

    private async Task SubscribeToLobbyEvents(string lobbyID)
    {
        var lobbyEventCallbacks = new LobbyEventCallbacks();

        lobbyEventCallbacks.KickedFromLobby += () =>
        {
            partyLobby = null;
            PlayerClothesPreloader.Instance.ClearAllModels();
            NetworkManager.Singleton.Shutdown();
            playerNetworkManagers.Clear();
            if (lobbyUI != null)
            {
                lobbyUI.ShowMainMenuUI();
            }
        };

        lobbyEventCallbacks.LobbyChanged += (changes) =>
        {
            changes.ApplyToLobby(partyLobby);
            onLobbyUpdate?.Invoke(Lobby);
        };

        lobbyEventCallbacks.DataChanged += (changes) =>
        {
            onLobbyUpdate?.Invoke(Lobby);
        };

        try
        {
            lobbyEvents = await Lobbies.Instance.SubscribeToLobbyEventsAsync(lobbyID, lobbyEventCallbacks);
        }
        catch (LobbyServiceException ex)
        {
            switch (ex.Reason)
            {
                case LobbyExceptionReason.AlreadySubscribedToLobby: Debug.LogWarning($"Already subscribed to lobby[{Lobby.Id}]. We did not need to try and subscribe again. Exception Message: {ex.Message}"); break;
                case LobbyExceptionReason.SubscriptionToLobbyLostWhileBusy: Debug.LogError($"Subscription to lobby events was lost while it was busy trying to subscribe. Exception Message: {ex.Message}"); throw;
                case LobbyExceptionReason.LobbyEventServiceConnectionError: Debug.LogError($"Failed to connect to lobby events. Exception Message: {ex.Message}"); throw;
                default: Debug.LogError(ex.Message); return;
            }
        }
    }

    private async void HandleLobbyHeartBeat()
    {
        if (partyLobby != null)
        {
            if (partyLobby.HostId != localPlayer.Id)
            {
                return;
            }

            lobbyHeartBeatTimer -= Time.deltaTime;
            if (lobbyHeartBeatTimer <= 0)
            {
                lobbyHeartBeatTimer = 15.0f;
                await LobbyService.Instance.SendHeartbeatPingAsync(partyLobby.Id);
            }
        }
    }   

    private async void HandleLobbyPollForUpdates()
    {
        if (partyLobby != null)
        {
            lobbyUpdateTimer -= Time.deltaTime;
            if (lobbyUpdateTimer <= 0)
            {
                lobbyUpdateTimer = 1.1f;
                partyLobby = await LobbyService.Instance.GetLobbyAsync(partyLobby.Id);
                localPlayer = partyLobby.Players.Find(player => player.Id == localPlayer.Id);

                SyncLobbyTypeFromLobby();

                onLobbyUpdate?.Invoke(Lobby);
            }
        }
    }

    private async Task<JoinAllocation> JoinRelay(string joinCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            return joinAllocation;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError(e);
            return default;
        }
    }

    public async void CreateLobby(LobbyType type = LobbyType.Custom)
    {
        try
        {
            LoadingScreen.Instance.ShowGenericLoadingScreen();
            var partyLobbyOptions = new CreateLobbyOptions()
            {
                IsPrivate = true,
                Player = localPlayer,
                Data = new Dictionary<string, DataObject>
                {
                    {"SelectedMap", new DataObject(DataObject.VisibilityOptions.Public, NumberEncoder.ToBase64<short>(0))},
                    {"LobbyType", new DataObject(DataObject.VisibilityOptions.Public, type.ToString())}
                },
            };
            var partyLobbyName = $"{LobbyNamePrefix}_{localPlayer.Id}";
            int maxPlayers = (type == LobbyType.Standards)
                ? CharactersList.Instance.GetMapFromID(0).maxTeamSize
                : CharactersList.Instance.GetMapFromID(0).maxTeamSize * 2;
            partyLobby = await LobbyService.Instance.CreateLobbyAsync(partyLobbyName, maxPlayers, partyLobbyOptions);
            currentLobbyType = type;
            Debug.Log($"Joined lobby: {partyLobby.Name}, code: {partyLobby.LobbyCode}");

            Allocation allocation = await AllocateRelay();
            string relayJoinCode = await GetRelayJoinCode(allocation);

            await LobbyService.Instance.UpdateLobbyAsync(partyLobby.Id, new UpdateLobbyOptions()
            {
                Data = new Dictionary<string, DataObject>
                {
                    {"RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode)}
                }
            });

            await SubscribeToLobbyEvents(partyLobby.Id);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, connectionType));

            if (NetworkManager.Singleton.StartHost())
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent += LoadingScreen.Instance.OnSceneEvent;
                NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
            }
            lobbyUI.ShowLobbyUI();

            onLobbyUpdate?.Invoke(partyLobby);

            LoadingScreen.Instance.HideGenericLoadingScreen();
        }
        catch (LobbyServiceException e)
        {
            LoadingScreen.Instance.HideGenericLoadingScreen();
            Debug.LogError($"Failed to create party lobby: {e.Message}");
        }
    }

    public async void TryLobbyJoin(string joinCode, bool useID = false)
    {
        try
        {
            LoadingScreen.Instance.ShowGenericLoadingScreen();


            if (useID)
            {
                var joinOptions = new JoinLobbyByIdOptions()
                {
                    Player = localPlayer
                };

                partyLobby = await LobbyService.Instance.JoinLobbyByIdAsync(joinCode, joinOptions);
            }
            else
            {
                var joinOptions = new JoinLobbyByCodeOptions()
                {
                    Player = localPlayer
                };

                partyLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(joinCode, joinOptions);
            }

            JoinAllocation joinAllocation = await JoinRelay(partyLobby.Data["RelayJoinCode"].Value);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, connectionType));
            Debug.Log($"Joined lobby: {partyLobby.Name}");

            await SubscribeToLobbyEvents(partyLobby.Id);

            CheckIfShouldChangePos(CharactersList.Instance.GetCurrentLobbyMap().maxTeamSize);
            if (NetworkManager.Singleton.StartClient())
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent += LoadingScreen.Instance.OnSceneEvent;
                NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
            }
            lobbyUI.ShowLobbyUI();

            onLobbyUpdate?.Invoke(partyLobby);

            LoadingScreen.Instance.HideGenericLoadingScreen();
        }
        catch (LobbyServiceException e)
        {
            LoadingScreen.Instance.HideGenericLoadingScreen();
            Debug.LogError($"Failed to join party lobby: {e.Message}");
        }
    }

    public async Task<bool> QuickJoin()
    {
        try
        {
            QuickJoinLobbyOptions options = new QuickJoinLobbyOptions();

            options.Player = localPlayer;

            /*options.Filter = new List<QueryFilter>()
            {
                new QueryFilter(
                    field: QueryFilter.FieldOptions.MaxPlayers,
                    op: QueryFilter.OpOptions.GE,
                    value: "10")
             };*/

            partyLobby = await LobbyService.Instance.QuickJoinLobbyAsync(options);

            LoadingScreen.Instance.ShowGenericLoadingScreen();

            JoinAllocation joinAllocation = await JoinRelay(partyLobby.Data["RelayJoinCode"].Value);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, connectionType));
            Debug.Log($"Joined lobby: {partyLobby.Name}");

            await SubscribeToLobbyEvents(partyLobby.Id);

            CheckIfShouldChangePos(CharactersList.Instance.GetCurrentLobbyMap().maxTeamSize);
            if (NetworkManager.Singleton.StartClient())
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent += LoadingScreen.Instance.OnSceneEvent;
                NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
            }
            lobbyUI.ShowLobbyUI();

            onLobbyUpdate?.Invoke(partyLobby);

            LoadingScreen.Instance.HideGenericLoadingScreen();
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning(e);
            return false;
        }
    }

    public async Task<QueryResponse> GetOpenLobbies()
    {
        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions();

            var lobbies = await LobbyService.Instance.QueryLobbiesAsync(options);

            return lobbies;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);

            return default;
        }
    }

    public void ReturnToOriginalLobbyAfterMatch()
    {
        if (!isInMatchmakingGame)
            return;

        isInMatchmakingGame = false;

        if (!string.IsNullOrEmpty(originalLobbyId))
        {
            TryLobbyJoin(originalLobbyId, useID: true);
        }
        else if (!string.IsNullOrEmpty(originalLobbyCode))
        {
            TryLobbyJoin(originalLobbyCode, useID: false);
        }
        else
        {
            lobbyUI.ShowMainMenuUI();
        }
    }

    public void OnMatchmakingGameEnded()
    {
        ReturnToOriginalLobbyAfterMatch();
    }

    private async Task PollMatchmakerTicket()
    {
        if (!isPartyLeader)
        {
            return;
        }

        Unity.Services.Matchmaker.Models.TicketStatusResponse ticketStatusResponse = await MatchmakerService.Instance.GetTicketAsync(partyMatchmakerTicketId);

        if (ticketStatusResponse == null)
        {
            return;
        }

        var matchIdAssignment = ticketStatusResponse.Value as Unity.Services.Matchmaker.Models.MatchIdAssignment;

        Debug.Log($"{matchIdAssignment.Status}; {matchIdAssignment.AssignmentType}; {matchIdAssignment.Message}; {matchIdAssignment.MatchId}");

        switch (matchIdAssignment.Status)
        {
            case Unity.Services.Matchmaker.Models.MatchIdAssignment.StatusOptions.Timeout:
                Debug.Log("Timeout");
                isSearching = false;
                break;
            case Unity.Services.Matchmaker.Models.MatchIdAssignment.StatusOptions.Failed:
                createTicketResponse = null;
                Debug.LogError("Failed");
                isSearching = false;
                break;
            case Unity.Services.Matchmaker.Models.MatchIdAssignment.StatusOptions.InProgress:
                break;
            case Unity.Services.Matchmaker.Models.MatchIdAssignment.StatusOptions.Found:
                isSearching = false;
                Debug.Log("Found");

                NetworkManager.Singleton.Shutdown();

                TryCreateOrLobbyJoin(matchIdAssignment.MatchId, 10);
                break;
            default:
                break;
        }

        Debug.Log(ticketStatusResponse.Type);
    }

    public async void TryCreateOrLobbyJoin(string lobbyID, int maxPlayers)
    {
        try
        {
            LoadingScreen.Instance.ShowGenericLoadingScreen();

            var partyLobbyOptions = new CreateLobbyOptions()
            {
                IsPrivate = true,
                Player = localPlayer,
                Data = new Dictionary<string, DataObject>
                {
                    {"SelectedMap", new DataObject(DataObject.VisibilityOptions.Member, NumberEncoder.ToBase64<short>(0))},
                    {"LobbyType", new DataObject(DataObject.VisibilityOptions.Public, LobbyType.Custom.ToString())}
                },
            };

            var partyLobbyName = $"{LobbyNamePrefix}_{localPlayer.Id}";
            partyLobby = await LobbyService.Instance.CreateOrJoinLobbyAsync(lobbyID, $"MATCH_{lobbyID}", maxPlayers, partyLobbyOptions);
            Debug.Log($"Joined lobby: {partyLobby.Name}, code: {partyLobby.LobbyCode}");

            await SubscribeToLobbyEvents(partyLobby.Id);

            if (partyLobby.HostId == Player.Id)
            {
                Allocation allocation = await AllocateRelay();
                string relayJoinCode = await GetRelayJoinCode(allocation);

                await LobbyService.Instance.UpdateLobbyAsync(partyLobby.Id, new UpdateLobbyOptions()
                {
                    Data = new Dictionary<string, DataObject>
                    {
                    {"RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode)}
                    }
                });

                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, connectionType));

                if (NetworkManager.Singleton.StartHost())
                {
                    NetworkManager.Singleton.SceneManager.OnSceneEvent += LoadingScreen.Instance.OnSceneEvent;
                    NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
                }
            }
            else
            {
                while (!partyLobby.Data.ContainsKey("RelayJoinCode"))
                {
                    await Task.Delay(100);
                }

                JoinAllocation joinAllocation = await JoinRelay(partyLobby.Data["RelayJoinCode"].Value);
                NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, connectionType));

                CheckIfShouldChangePos(CharactersList.Instance.GetCurrentLobbyMap().maxTeamSize);
                if (NetworkManager.Singleton.StartClient())
                {
                    NetworkManager.Singleton.SceneManager.OnSceneEvent += LoadingScreen.Instance.OnSceneEvent;
                    NetworkManager.Singleton.SceneManager.OnSceneEvent += OnSceneEvent;
                }
            }


            lobbyUI.ShowLobbyUI();

            onLobbyUpdate?.Invoke(partyLobby);

            await Task.Delay(2500);

            if (partyLobby.HostId == Player.Id)
            {
                StartGame();
            }

            LoadingScreen.Instance.HideGenericLoadingScreen();
        }
        catch (LobbyServiceException e)
        {
            LoadingScreen.Instance.HideGenericLoadingScreen();
            Debug.LogError($"Failed to create party lobby: {e.Message}");
        }
    }

    private IEnumerator PollMatchmakerTicketRoutine()
    {
        while (isSearching && isPartyLeader)
        {
            Task searchTask = PollMatchmakerTicket();


            yield return new WaitUntil(() => searchTask.IsCompleted);

            yield return new WaitForSeconds(1.1f);
        }
        lobbyUI.HideMatchmakingBarUI();
    }

    public async void FindMatch()
    {
        if (isSearching)
        {
            return;
        }

        if (!isPartyLeader)
        {
            Debug.Log("Solo il leader del party può avviare il matchmaking.");
            return;
        }

        originalPartyLobby = partyLobby;
        originalLobbyId = partyLobby?.Id;
        originalLobbyCode = partyLobby?.LobbyCode;
        isInMatchmakingGame = false;

        var matchmakerPlayers = GetMatchmakerPlayers();

        createTicketResponse = await MatchmakerService.Instance.CreateTicketAsync(matchmakerPlayers, new CreateTicketOptions("StandardsQueue"));
        partyMatchmakerTicketId = createTicketResponse.Id;

        isSearching = true;

        lobbyUI.ShowMatchmakingBarUI(CancelMatchmaking);

        StartCoroutine(PollMatchmakerTicketRoutine());
    }

    public void CancelMatchmaking()
    {
        if (createTicketResponse == null)
        {
            return;
        }

        isSearching = false;
        createTicketResponse = null;

        lobbyUI.HideMatchmakingBarUI();
    }

    public void CheckIfShouldChangePos(int maxTeamSize)
    {
        HashSet<string> usedPositions = new HashSet<string>();

        foreach (var player in partyLobby.Players)
        {
            if (player.Id == localPlayer.Id)
            {
                continue;
            }
            string playerTeam = player.Data["PlayerTeam"].Value;
            string playerPos = NumberEncoder.FromBase64<short>(player.Data["PlayerPos"].Value).ToString();
            usedPositions.Add(playerTeam + playerPos);
        }

        string localTeam = localPlayer.Data["PlayerTeam"].Value;
        short localPos = NumberEncoder.FromBase64<short>(localPlayer.Data["PlayerPos"].Value);

        if (localPos >= maxTeamSize)
        {
            localPos = (short)(maxTeamSize - 1);
            UpdatePlayerTeamAndPos(localTeam, localPos);
        }

        if (usedPositions.Contains(localTeam + localPos.ToString()))
        {
            for (int i = 0; i < maxTeamSize * 2; i++)
            {
                string team = i < maxTeamSize ? "Blue" : "Orange";
                short pos = (short)(i % maxTeamSize);

                if (!usedPositions.Contains(team + pos.ToString()))
                {
                    UpdatePlayerTeamAndPos(team, pos);
                    break;
                }
            }
        }
    }


    private async void UpdatePlayerData(UpdatePlayerOptions options)
    {
        if (Lobby == null || localPlayer == null)
        {
            return;
        }

        try
        {
            await LobbyService.Instance.UpdatePlayerAsync(Lobby.Id, localPlayer.Id, options);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    private async void UpdateLobbyData(UpdateLobbyOptions options)
    {
        if (Lobby == null)
        {
            return;
        }

        try
        {
            await LobbyService.Instance.UpdateLobbyAsync(Lobby.Id, options);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    private async Task<string> GetRelayJoinCode(Allocation allocation)
    {
        try
        {
            var relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"Relay join code: {relayJoinCode}");
            return relayJoinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError(e);
            return default;
        }
    }

    public async void KickPlayer(string playerID)
    {
        if (partyLobby.HostId != localPlayer.Id)
        {
            return;
        }

        await RemoveFromParty(playerID);
    }

    public void PlayerSwitchTeam()
    {
        UpdatePlayerOptions options = new UpdatePlayerOptions();
        options.Data = localPlayer.Data;

        if (options.Data["PlayerTeam"].Value == "Blue")
        {
            options.Data["PlayerTeam"].Value = "Orange";
        }
        else
        {
            options.Data["PlayerTeam"].Value = "Blue";
        }

        UpdatePlayerData(options);

        Debug.Log($"Switched team to {localPlayer.Data["PlayerTeam"].Value}");
    }

    public void UpdatePlayerTeamAndPos(string team, short pos)
    {
        UpdatePlayerOptions options = new UpdatePlayerOptions();
        options.Data = localPlayer.Data;
        options.Data["PlayerTeam"].Value = team;
        options.Data["PlayerPos"].Value = NumberEncoder.ToBase64(pos);
        Debug.Log($"Changed team to {options.Data["PlayerTeam"].Value} and pos to {options.Data["PlayerPos"].Value}");

        UpdatePlayerData(options);
    }

    public void UpdatePlayerClothes(PlayerClothesInfo clothesInfo)
    {
        UpdatePlayerOptions options = new UpdatePlayerOptions();
        options.Data = localPlayer.Data;
        options.Data["ClothingInfo"].Value = clothesInfo.Serialize();
        Debug.Log($"Changed clothes to {options.Data["ClothingInfo"].Value}");

        localPlayer.Data = options.Data;

        UpdatePlayerData(options);
    }

    public void UpdatePlayerHeldItems(string heldItems)
    {
        UpdatePlayerOptions options = new UpdatePlayerOptions();
        options.Data = localPlayer.Data;
        options.Data["HeldItems"].Value = heldItems;
        Debug.Log($"Changed held items to {options.Data["HeldItems"].Value}");

        UpdatePlayerData(options);
    }

    private IEnumerator UpdatePlayerOwnerID(ulong id)
    {
        UpdatePlayerOptions options = new UpdatePlayerOptions();
        options.Data = localPlayer.Data;
        options.Data["OwnerID"].Value = id.ToString();

        Debug.Log($"Changed owner ID to {options.Data["OwnerID"].Value}");
        yield return null;
        UpdatePlayerData(options);
    }
    
    public void ChangePlayerCharacter(short characterID)
    {
        UpdatePlayerOptions options = new UpdatePlayerOptions();
        options.Data = localPlayer.Data;
        options.Data["SelectedCharacter"].Value = NumberEncoder.ToBase64(characterID);
        Debug.Log($"Changed character to {options.Data["SelectedCharacter"].Value}");

        UpdatePlayerData(options);
    }

    public void ChangePlayerBattleItem(string battleItemID)
    {
        UpdatePlayerOptions options = new UpdatePlayerOptions();
        options.Data = localPlayer.Data;
        options.Data["BattleItem"].Value = battleItemID;
        Debug.Log($"Changed Battle Item to {options.Data["BattleItem"].Value}");

        UpdatePlayerData(options);
    }

    public void ChangePlayerName(string newName)
    {
        UpdatePlayerOptions options = new UpdatePlayerOptions();
        options.Data = localPlayer.Data;
        options.Data["PlayerName"].Value = newName;
        Debug.Log($"Changed name to {options.Data["PlayerName"].Value}");

        UpdatePlayerData(options);
    }

    public void ChangeLobbyVisibility(bool isPrivate)
    {
        if (partyLobby.HostId != localPlayer.Id)
        {
            return;
        }

        UpdateLobbyOptions options = new UpdateLobbyOptions();
        options.Data = partyLobby.Data;
        options.MaxPlayers = partyLobby.MaxPlayers;
        options.HostId = partyLobby.HostId;
        options.Name = partyLobby.Name;
        options.IsLocked = partyLobby.IsLocked;
        options.IsPrivate = isPrivate;

        UpdateLobbyData(options);
    }

    public void SetLobbyLockedAndPrivate(bool isLocked)
    {
        if (partyLobby.HostId != localPlayer.Id)
        {
            return;
        }

        UpdateLobbyOptions options = new UpdateLobbyOptions();
        options.Data = partyLobby.Data;
        options.MaxPlayers = partyLobby.MaxPlayers;
        options.HostId = partyLobby.HostId;
        options.Name = partyLobby.Name;
        options.IsPrivate = isLocked;
        options.IsLocked = isLocked;

        UpdateLobbyData(options);
    }

    public void SetLobbyLocked(bool isLocked)
    {
        if (partyLobby.HostId != localPlayer.Id)
        {
            return;
        }

        UpdateLobbyOptions options = new UpdateLobbyOptions();
        options.Data = partyLobby.Data;
        options.MaxPlayers = partyLobby.MaxPlayers;
        options.HostId = partyLobby.HostId;
        options.Name = partyLobby.Name;
        options.IsPrivate = partyLobby.IsPrivate;
        options.IsLocked = isLocked;

        UpdateLobbyData(options);
    }

    public void ChangeLobbyMap(MapInfo map)
    {
        if (partyLobby.HostId != localPlayer.Id)
        {
            return;
        }

        UpdateLobbyOptions options = new UpdateLobbyOptions();
        options.Data = partyLobby.Data;
        options.MaxPlayers = map.maxTeamSize*2;
        options.HostId = partyLobby.HostId;
        options.Name = partyLobby.Name;
        options.IsPrivate = partyLobby.IsPrivate;
        options.IsLocked = partyLobby.IsLocked;
        options.Data["SelectedMap"] = new DataObject(DataObject.VisibilityOptions.Public, NumberEncoder.ToBase64(CharactersList.Instance.GetMapID(map)));

        UpdateLobbyData(options);
    }

    private async Task<Allocation> AllocateRelay()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPartyMembers-1);

            return allocation;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError(e);

            return default;
        }
    }

    public async void LeaveLobby()
    {
        LoadingScreen.Instance.ShowGenericLoadingScreen();
        await RemoveFromParty(localPlayer.Id);
        partyLobby = null;
        PlayerClothesPreloader.Instance.ClearAllModels();
        NetworkManager.Singleton.Shutdown();

        playerNetworkManagers.Clear();

        lobbyUI.ShowMainMenuUI();
        LoadingScreen.Instance.HideGenericLoadingScreen();
    }

    private async void LeaveLobbyNoGUI()
    {
        await RemoveFromParty(localPlayer.Id);
        partyLobby = null;
        PlayerClothesPreloader.Instance.ClearAllModels();
        NetworkManager.Singleton.Shutdown();
        playerNetworkManagers.Clear();
    }

    private async Task RemoveFromParty(string playerID)
    {
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(partyLobby.Id, playerID);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    public void StartGame()
    {
        if (partyLobby.HostId != localPlayer.Id)
        {
            return;
        }

        if (IsStandardsLobby())
        {
            FindMatch();
            return;
        }

        SetLobbyLockedAndPrivate(true);

        string mapMode = CharactersList.Instance.GetCurrentLobbyMap().characterSelectType switch
        {
            CharacterSelectType.BlindPick => "CharacterSelect",
            CharacterSelectType.Draft => "DraftSelect",
            CharacterSelectType.PsyduckRacing => "RacingReadyScreen",
            _ => "CharacterSelect",
        };

        NetworkManager.Singleton.SceneManager.LoadScene(mapMode, LoadSceneMode.Single);
    }

    public void LoadGameMap()
    {
        if (partyLobby.HostId != localPlayer.Id)
        {
            return;
        }
        MapInfo selectedMap = CharactersList.Instance.GetMapFromID(NumberEncoder.FromBase64<short>(partyLobby.Data["SelectedMap"].Value));
        NetworkManager.Singleton.SceneManager.LoadScene(selectedMap.sceneName, LoadSceneMode.Single);
    }

    public void LoadResultsScreen()
    {
        if (partyLobby.HostId != localPlayer.Id)
        {
            return;
        }

        SetLobbyLocked(false);

        LoadingScreen.Instance.ShowGenericLoadingScreen();

        NetworkManager.Singleton.SceneManager.LoadScene("LobbyScene", LoadSceneMode.Single);
    }

    private IEnumerator LoadResultsScreenAsync()
    {
        string resultScreen = CharactersList.Instance.GetCurrentLobbyMap().characterSelectType == CharacterSelectType.PsyduckRacing ? "RacingGameResults" : "GameResults";

        var task = SceneManager.LoadSceneAsync(resultScreen, LoadSceneMode.Additive);
        while (!task.isDone)
        {
            yield return null;
        }

        lobbyUI.DisableLobbyScene();
    }

    public void ReturnToLobby(bool leaveLobby)
    {
        if (leaveLobby) LeaveLobbyNoGUI();
        StartCoroutine(LoadLobbyAsync());
    }

    public void ReturnToHomeWithoutLeavingLobby()
    {
        StartCoroutine(ReturnToHomeWithoutLeavingLobbyAsync());
    }

    private IEnumerator ReturnToHomeWithoutLeavingLobbyAsync()
    {
        LoadingScreen.Instance.ShowGenericLoadingScreen();
        string resultScreen = CharactersList.Instance.GetCurrentLobbyMap().characterSelectType == CharacterSelectType.PsyduckRacing ? "RacingGameResults" : "GameResults";

        var loadTask = SceneManager.UnloadSceneAsync(resultScreen);

        yield return loadTask;

        lobbyUI.EnableLobbyScene();

        yield return new WaitForSeconds(0.1f);

        lobbyUI.ShowLobbyUI();

        LoadingScreen.Instance.HideGenericLoadingScreen();
    }

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        switch (sceneEvent.SceneEventType)
        {
            case SceneEventType.LoadComplete:
                if (sceneEvent.SceneName == "LobbyScene" && loadResultsScreen)
                {
                    StartCoroutine(LoadResultsScreenAsync());
                    loadResultsScreen = false;
                }
                break;
        }
    }

    public Player GetPlayerByID(string playerID)
    {
        foreach (var player in partyLobby.Players)
        {
            if (player.Id == playerID)
            {
                return player;
            }
        }

        return null;
    }

    public Player[] GetTeamPlayers(Team team)
    {
        List<Player> teamPlayers = new List<Player>();
        foreach (var player in partyLobby.Players)
        {
            if (TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value) == team)
            {
                teamPlayers.Add(player);
            }
        }

        teamPlayers.Sort((p1, p2) =>
        {
            short pos1 = NumberEncoder.FromBase64<short>(p1.Data["PlayerPos"].Value);
            short pos2 = NumberEncoder.FromBase64<short>(p2.Data["PlayerPos"].Value);
            return pos1.CompareTo(pos2);
        });

        return teamPlayers.ToArray();
    }

    public Team GetLocalPlayerTeam()
    {

        string team = localPlayer.Data["PlayerTeam"].Value.ToLower();

        switch (team)
        {
            case "blue":
                return Team.Blue;
            case "orange":
                return Team.Orange;
            default:
                return Team.Neutral;
        }
    }

    public bool IsPlayerInResultScreen(Player player)
    {
        foreach (var playerNetworkManager in playerNetworkManagers)
        {
            if (playerNetworkManager.LocalPlayer.Id == player.Id)
            {
                return playerNetworkManager.IsInResultScreen;
            }
        }

        return false;
    }

    public bool IsAnyPlayerInResultScreen()
    {
        foreach (var playerNetworkManager in playerNetworkManagers)
        {
            if (playerNetworkManager.IsInResultScreen)
            {
                return true;
            }
        }

        return false;
    }

    public List<Unity.Services.Matchmaker.Models.Player> GetMatchmakerPlayers()
    {
        List<Unity.Services.Matchmaker.Models.Player> players = new List<Unity.Services.Matchmaker.Models.Player>();
        foreach (var player in partyLobby.Players)
        {
            players.Add(new Unity.Services.Matchmaker.Models.Player(player.Id, new Dictionary<string, string>()));
        }
        return players;
    }

    public void ChangeLobbyType(LobbyType newType)
    {
        if (partyLobby == null || partyLobby.HostId != localPlayer.Id)
            return;

        if (currentLobbyType == newType)
            return;

        currentLobbyType = newType;

        UpdateLobbyOptions options = new UpdateLobbyOptions();
        options.Data = partyLobby.Data;
        options.MaxPlayers = (newType == LobbyType.Standards)
            ? CharactersList.Instance.GetCurrentLobbyMap().maxTeamSize
            : CharactersList.Instance.GetCurrentLobbyMap().maxTeamSize * 2;
        options.HostId = partyLobby.HostId;
        options.Name = partyLobby.Name;
        options.IsPrivate = partyLobby.IsPrivate;
        options.IsLocked = partyLobby.IsLocked;

        if (options.Data.ContainsKey("LobbyType"))
            options.Data["LobbyType"] = new DataObject(DataObject.VisibilityOptions.Public, newType.ToString());
        else
            options.Data.Add("LobbyType", new DataObject(DataObject.VisibilityOptions.Public, newType.ToString()));

        UpdateLobbyData(options);

        Debug.Log($"Changed lobby type to {newType}");
    }

    private void SyncLobbyTypeFromLobby()
    {
        if (partyLobby != null && partyLobby.Data != null && partyLobby.Data.ContainsKey("LobbyType"))
        {
            if (Enum.TryParse<LobbyType>(partyLobby.Data["LobbyType"].Value, out var parsedType))
            {
                currentLobbyType = parsedType;
            }
        }
    }

    public bool IsCustomLobby() => currentLobbyType == LobbyType.Custom;
    public bool IsStandardsLobby() => currentLobbyType == LobbyType.Standards;

    public int GetMaxPartyMembers()
    {
        if (IsStandardsLobby())
            return CharactersList.Instance.GetCurrentLobbyMap().maxTeamSize;
        else
            return CharactersList.Instance.GetCurrentLobbyMap().maxTeamSize * 2;
    }

    private void OnClientDisconnectFromServer(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId) return;

        if (NetworkManager.Singleton.IsClient && clientId == (ulong.TryParse(GetPlayerByID(Lobby.HostId).Data["OwnerID"].Value, out ulong localPlayerId) ? localPlayerId : 69420))
        {
            ReturnToLobby(true);
        }
    }
}
