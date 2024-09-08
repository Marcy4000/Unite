using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

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

#if UNITY_WEBGL
    private string connectionType = "wss";
#else
    private string connectionType = "dtls";
#endif

    public Lobby Lobby => partyLobby;
    public Player Player => localPlayer;

    public GameResults GameResults { get => gameResults; set => gameResults = value;}

    public ILobbyEvents LobbyEvents => lobbyEvents;

    public event Action<Lobby> onLobbyUpdate;

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
        NetworkManager.Singleton.OnClientConnectedCallback += (clientId) =>
        {
            if (clientId != NetworkManager.Singleton.LocalClientId)
            {
                return;
            }
            StartCoroutine(UpdatePlayerOwnerID(clientId));
        };
    }

    public void StartGame(string playerName)
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            return;
        }

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

            options.SetProfile(UnityEngine.Random.Range(0,1000).ToString());

            await UnityServices.InitializeAsync(options);

            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            PlayerClothesInfo clothes;

            try
            {
                clothes = PlayerPrefs.HasKey("ClothingInfo") ? PlayerClothesInfo.Deserialize(PlayerPrefs.GetString("ClothingInfo")) : new PlayerClothesInfo();
            }
            catch (Exception)
            {
                clothes = new PlayerClothesInfo();
            }

            localPlayer = new Player(AuthenticationService.Instance.PlayerId, AuthenticationService.Instance.Profile, new Dictionary<string, PlayerDataObject>
            {
                {"PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, localPlayerName)},
                {"OwnerID", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "0")},
                {"PlayerTeam", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "Blue")},
                {"PlayerPos", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, NumberEncoder.ToBase64<short>(0))},
                {"SelectedCharacter", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "")},
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

    private async Task SubscribeToLobbyEvents(string lobbyID)
    {
        var lobbyEventCallbacks = new LobbyEventCallbacks();

        lobbyEventCallbacks.KickedFromLobby += () =>
        {
            partyLobby = null;
            NetworkManager.Singleton.Shutdown();
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

    public async void CreateLobby()
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
                    {"SelectedMap", new DataObject(DataObject.VisibilityOptions.Member, "RemoatStadium")}
                },
            };
            var partyLobbyName = $"{LobbyNamePrefix}_{localPlayer.Id}";
            partyLobby = await LobbyService.Instance.CreateLobbyAsync(partyLobbyName, maxPartyMembers, partyLobbyOptions);
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

    public async void TryLobbyJoin(string joinCode)
    {
        try
        {
            LoadingScreen.Instance.ShowGenericLoadingScreen();
            var joinOptions = new JoinLobbyByCodeOptions()
            {
                Player = localPlayer
            };

            partyLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(joinCode, joinOptions);

            JoinAllocation joinAllocation = await JoinRelay(partyLobby.Data["RelayJoinCode"].Value);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, connectionType));
            Debug.Log($"Joined lobby: {partyLobby.Name}");

            await SubscribeToLobbyEvents(partyLobby.Id);

            CheckIfShouldChangePos(CharactersList.Instance.GetCurrentLobbyMap().maxTeamSize);
            if (NetworkManager.Singleton.StartClient())
            {
                NetworkManager.Singleton.SceneManager.OnSceneEvent += LoadingScreen.Instance.OnSceneEvent;
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

    public void CheckIfShouldChangePos(int maxTeamSize)
    {
        HashSet<string> usedPositions = new HashSet<string>();

        // Track all used positions
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

        // Clamp the local player's position within the valid range
        if (localPos >= maxTeamSize)
        {
            localPos = (short)(maxTeamSize - 1);
            UpdatePlayerTeamAndPos(localTeam, localPos);
        }

        // Check if local player's position is already taken
        if (usedPositions.Contains(localTeam + localPos.ToString()))
        {
            // Find the next available position
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
    
    public void ChangePlayerCharacter(string characterName)
    {
        UpdatePlayerOptions options = new UpdatePlayerOptions();
        options.Data = localPlayer.Data;
        options.Data["SelectedCharacter"].Value = characterName;
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
        options.IsPrivate = isPrivate;

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
        options.Data["SelectedMap"] = new DataObject(DataObject.VisibilityOptions.Member, map.sceneName);

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
        NetworkManager.Singleton.Shutdown();
        lobbyUI.ShowMainMenuUI();
        LoadingScreen.Instance.HideGenericLoadingScreen();
    }

    private async void LeaveLobbyNoGUI()
    {
        await RemoveFromParty(localPlayer.Id);
        partyLobby = null;
        NetworkManager.Singleton.Shutdown();
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
        ChangeLobbyVisibility(true);
        NetworkManager.Singleton.SceneManager.LoadScene("CharacterSelect", LoadSceneMode.Single);
        //NetworkManager.Singleton.SceneManager.LoadScene("DraftSelect", LoadSceneMode.Single);
    }

    public void LoadGameMap()
    {
        if (partyLobby.HostId != localPlayer.Id)
        {
            return;
        }
        string selectedMap = partyLobby.Data["SelectedMap"].Value;
        NetworkManager.Singleton.SceneManager.LoadScene(selectedMap, LoadSceneMode.Single);
    }

    public void LoadResultsScreen()
    {
        if (partyLobby.HostId != localPlayer.Id)
        {
            return;
        }
        NetworkManager.Singleton.SceneManager.LoadScene("GameResults", LoadSceneMode.Single);
    }

    public void ReturnToLobby(bool leaveLobby)
    {
        if (leaveLobby) LeaveLobbyNoGUI();
        SceneManager.LoadSceneAsync("LobbyScene");
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

    public Player[] GetTeamPlayers(bool orangeTeam)
    {
        List<Player> teamPlayers = new List<Player>();
        foreach (var player in partyLobby.Players)
        {
            if (player.Data["PlayerTeam"].Value == (orangeTeam ? "Orange" : "Blue"))
            {
                teamPlayers.Add(player);
            }
        }

        // Sort the players by their position
        teamPlayers.Sort((p1, p2) =>
        {
            short pos1 = NumberEncoder.FromBase64<short>(p1.Data["PlayerPos"].Value);
            short pos2 = NumberEncoder.FromBase64<short>(p2.Data["PlayerPos"].Value);
            return pos1.CompareTo(pos2);
        });

        return teamPlayers.ToArray();
    }

    public bool GetLocalPlayerTeam()
    {
        return localPlayer.Data["PlayerTeam"].Value == "Orange";
    }
}
