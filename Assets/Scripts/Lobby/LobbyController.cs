using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
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

    private string testPlayerName;

    private GameResults gameResults;

    public Lobby Lobby => partyLobby;
    public Player Player => localPlayer;

    public GameResults GameResults { get => gameResults; set => gameResults = value;}

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

        testPlayerName = $"TestPlayer {UnityEngine.Random.Range(0, 1000)}";
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
            options.SetProfile(UnityEngine.Random.Range(0,1000).ToString());

            await UnityServices.InitializeAsync(options);

            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            localPlayer = new Player(AuthenticationService.Instance.PlayerId, AuthenticationService.Instance.Profile, new Dictionary<string, PlayerDataObject>
            {
                {"PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, testPlayerName)},
                {"PlayerTeam", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "Blue")},
                {"SelectedCharacter", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "Cinderace")}
            });

            LoadingScreen.Instance.HideGenericLoadingScreen();
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
                    {"BlueTeamScore", new DataObject(DataObject.VisibilityOptions.Member, "0")},
                    {"OrangeTeamScore", new DataObject(DataObject.VisibilityOptions.Member, "0")}
                }
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

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "dtls"));

            NetworkManager.Singleton.StartHost();
            lobbyUI.ShowLobbyUI();
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

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));
            Debug.Log($"Joined lobby: {partyLobby.Name}");
            NetworkManager.Singleton.StartClient();
            lobbyUI.ShowLobbyUI();
            LoadingScreen.Instance.HideGenericLoadingScreen();
        }
        catch (LobbyServiceException e)
        {
            LoadingScreen.Instance.HideGenericLoadingScreen();
            Debug.LogError($"Failed to join party lobby: {e.Message}");
        }
    }

    private async void UpdatePlayerData(UpdatePlayerOptions options)
    {
        try
        {
            await LobbyService.Instance.UpdatePlayerAsync(Lobby.Id, localPlayer.Id, options);
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

    public void ChangePlayerCharacter(string characterName)
    {
        UpdatePlayerOptions options = new UpdatePlayerOptions();
        options.Data = localPlayer.Data;
        options.Data["SelectedCharacter"].Value = characterName;
        Debug.Log($"Changed character to {options.Data["SelectedCharacter"]}");

        UpdatePlayerData(options);
    }

    public void ChangePlayerName(string newName)
    {
        UpdatePlayerOptions options = new UpdatePlayerOptions();
        options.Data = localPlayer.Data;
        options.Data["PlayerName"].Value = newName;
        Debug.Log($"Changed name to {options.Data["PlayerName"]}");

        UpdatePlayerData(options);
    }

    public async void UpdateLobbyScores(int blueTeamScore, int orangeTeamScore)
    {
        UpdateLobbyOptions options = new UpdateLobbyOptions();

        options.HostId = partyLobby.HostId;
        options.MaxPlayers = partyLobby.MaxPlayers;
        options.IsPrivate = partyLobby.IsPrivate;
        options.Name = partyLobby.Name;

        options.Data = new Dictionary<string, DataObject>
        {
            {"BlueTeamScore", new DataObject(DataObject.VisibilityOptions.Member, blueTeamScore.ToString())},
            {"OrangeTeamScore", new DataObject(DataObject.VisibilityOptions.Member, orangeTeamScore.ToString())}
        };

        try
        {
            await LobbyService.Instance.UpdateLobbyAsync(partyLobby.Id, options);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
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
        NetworkManager.Singleton.SceneManager.LoadScene("CharacterSelect", LoadSceneMode.Single);
    }

    public void LoadRemoat()
    {
        NetworkManager.Singleton.SceneManager.LoadScene("RemoatStadium", LoadSceneMode.Single);
    }

    public void LoadResultsScreen()
    {
        NetworkManager.Singleton.SceneManager.LoadScene("GameResults", LoadSceneMode.Single);
    }

    public void ReturnToLobby()
    {
        LeaveLobbyNoGUI();
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
        return teamPlayers.ToArray();
    }
}
