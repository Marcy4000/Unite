using JSAM;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unite.Multiplayer;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum GameState
{
    Waiting,
    Initialising,
    Starting,
    Playing,
    Ended
}

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;
    public static event Action<GameManager> OnInstanceAvailable;

    public float MAX_GAME_TIME { get; private set; } = 600f;
    public float FINAL_STRETCH_TIME { get; private set; } = 120f;

    [SerializeField] private SurrenderManager surrenderManager;
    [SerializeField] private Button surrenderButton; //This shouldn't be here but eh

    private NetworkVariable<float> gameTime = new NetworkVariable<float>(600f);
    private NetworkVariable<ushort> blueTeamScore = new NetworkVariable<ushort>(0);
    private NetworkVariable<ushort> orangeTeamScore = new NetworkVariable<ushort>(0);

    private NetworkVariable<bool> finalStretch = new NetworkVariable<bool>(false);

    [SerializeField, Min(1f)] private float playerLoadingTimeoutSeconds = 120f;
    private readonly ClientReadinessBarrier loadingBarrier = new ClientReadinessBarrier(ClientLoadStage.Map | ClientLoadStage.Pokemon);
    private bool networkSceneLoaded;
    private bool startScheduled;
    private bool startRoutineStarted;
    private int contentLoadGeneration;
    private NetworkManager subscribedNetworkManager;
    private NetworkSceneManager subscribedSceneManager;

    private List<PlayerManager> players = new List<PlayerManager>();
    private LaneManager[] lanes;

    private List<ResultScoreInfo> blueTeamScores;
    private List<ResultScoreInfo> orangeTeamScores;

    private MapInfo currentMap;
    private readonly Queue<PassiveExpSetting> passiveExpQueue = new Queue<PassiveExpSetting>();
    private PassiveExpSetting currentPassiveExpSetting;

    public float GameTime => gameTime.Value;
    public int BlueTeamScore => blueTeamScore.Value;
    public int OrangeTeamScore => orangeTeamScore.Value;
    public bool FinalStretch => finalStretch.Value;

    private NetworkVariable<GameState> gameState = new NetworkVariable<GameState>(GameState.Waiting);

    public List<PlayerManager> Players => players;
    public LaneManager[] Lanes => lanes;

    public GameState GameState => gameState.Value;

    public MapInfo CurrentMap => currentMap;

    public event Action<int> onUpdatePassiveExp;
    public event Action<GameState> onGameStateChanged;
    public event Action<PlayerManager> OnPlayerAdded;
    public event Action<PlayerManager> OnPlayerRemoved;
    public event Action onFinalStretch;
    public event Action onFarmLevelUps;

    private BaseStartAnimationUI startAnimationUI;

    private AsyncOperationHandle<SceneInstance> loadHandle;
    private AsyncOperationHandle<IList<PokemonBase>> pokemonHandle;

    private void Awake()
    {
        Instance = this;
        OnInstanceAvailable?.Invoke(this);
    }

    private void OnDisable()
    {
        DetachNetworkEvents();
    }

    private void DetachNetworkEvents()
    {
        if (subscribedSceneManager != null)
        {
            subscribedSceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
            subscribedSceneManager = null;
        }
        if (subscribedNetworkManager != null)
        {
            subscribedNetworkManager.OnClientDisconnectCallback -= OnClientDisconnect;
            subscribedNetworkManager = null;
        }
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        loadingBarrier.Remove(clientId);
        for (var index = players.Count - 1; index >= 0; index--)
        {
            var player = players[index];
            if (player == null)
            {
                players.RemoveAt(index);
                OnPlayerRemoved?.Invoke(player);
            }
            else if (player.OwnerClientId == clientId) RemovePlayer(player);
        }
    }

    private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        MapInfo selectedMap = CharactersList.Instance.GetCurrentLobbyMap();
        if (sceneName.Equals(selectedMap.sceneName) && IsServer)
        {
            if (clientsTimedOut.Count > 0)
            {
                LobbyController.Instance?.ReportSessionFailure(SessionFailureReason.LoadingTimedOut);
                return;
            }
            networkSceneLoaded = true;
        }
    }

    private IEnumerator WaitForPlayersToLoad()
    {
        var deadline = Time.realtimeSinceStartupAsDouble + playerLoadingTimeoutSeconds;
        while (!networkSceneLoaded || !loadingBarrier.IsReady)
        {
            if (!IsSpawned || !IsServer) yield break;
            if (Time.realtimeSinceStartupAsDouble >= deadline)
            {
                Debug.LogWarning($"Match loading timed out; {loadingBarrier.PendingClients.Length} client(s) have not loaded required content.");
                LobbyController.Instance?.ReportSessionFailure(SessionFailureReason.LoadingTimedOut);
                yield break;
            }
            yield return null;
        }
        if (startScheduled || !IsSpawned || !IsServer) yield break;
        startScheduled = true;
        StartGameRPC();
    }

    private IEnumerator StartGameDelayed()
    {
        yield return new WaitForSeconds(0.1f);
        AudioManager.StopMusic(DefaultAudioMusic.LoadingTheme, null, true);
        AudioManager.PlaySound(DefaultAudioSounds.LoadComplete);
        if (IsServer)
        {
            gameState.Value = GameState.Initialising;
            LobbyController.Instance.BotRoster?.SpawnBotsForMatch();
        }
        var botDeadline = Time.realtimeSinceStartup + playerLoadingTimeoutSeconds;
        while (LobbyController.Instance.BotRoster?.HasBots == true &&
            (FindObjectsByType<PlayerManager>(FindObjectsSortMode.None).Count(player => player.IsBot && player.BotReady) !=
                LobbyController.Instance.Lobby.Players.Count(player => player.IsBot)))
        {
            if (Time.realtimeSinceStartup >= botDeadline)
            {
                LobbyController.Instance.ReportSessionFailure(SessionFailureReason.LoadingTimedOut);
                yield break;
            }
            yield return null;
        }
        yield return new WaitForSeconds(1.9f);
        
        // Find and subscribe to the start animation in the scene
        startAnimationUI = FindAnyObjectByType<BaseStartAnimationUI>();
        if (startAnimationUI != null)
        {
            startAnimationUI.OnAnimationComplete += OnStartAnimationComplete;
        }
        
        StartCoroutine(StartGameRoutine());
    }

    [Rpc(SendTo.Everyone)]
    private void StartGameRPC()
    {
        if (startRoutineStarted) return;
        startRoutineStarted = true;
        StartCoroutine(StartGameDelayed());
    }

    public override void OnNetworkSpawn()
    {
        currentMap = CharactersList.Instance.GetCurrentLobbyMap();
        var generation = ++contentLoadGeneration;
        networkSceneLoaded = false;
        startScheduled = false;
        startRoutineStarted = false;
        subscribedNetworkManager = NetworkManager;
        subscribedSceneManager = NetworkManager.SceneManager;
        subscribedSceneManager.OnLoadEventCompleted += HandleSceneLoaded;
        subscribedNetworkManager.OnClientDisconnectCallback += OnClientDisconnect;

        // Register the expected roster before cached Addressables operations can
        // complete and send readiness. Readiness is never inferred from an RPC
        // count, and messages from departed clients cannot add entries again.
        if (IsServer)
        {
            loadingBarrier.Reset(NetworkManager.ConnectedClientsIds);
            StartCoroutine(WaitForPlayersToLoad());
        }

        loadHandle = Addressables.LoadSceneAsync(currentMap.mapSceneKey, LoadSceneMode.Additive);

        pokemonHandle = Addressables.LoadAssetsAsync<PokemonBase>("characters", null);

        loadHandle.Completed += (handle) =>
        {
            if (!IsSpawned || generation != contentLoadGeneration) return;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                LobbyController.Instance?.ReportSessionFailure(SessionFailureReason.ContentLoadFailed);
                return;
            }
            NotifyPlayerLoadedMapRPC(NetworkManager.LocalClientId);
        };

        pokemonHandle.Completed += (handle) =>
        {
            if (!IsSpawned || generation != contentLoadGeneration) return;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                LobbyController.Instance?.ReportSessionFailure(SessionFailureReason.ContentLoadFailed);
                return;
            }
            NotifyPlayerLoadedPokemonsRPC(NetworkManager.LocalClientId);
        };

        LoadingScreen.Instance.SetLoadingManagerOperation(loadHandle);

        gameState.OnValueChanged += GameStateChanged;
        finalStretch.OnValueChanged += OnFinalStretchChanged;
        lanes = FindObjectsByType<LaneManager>(FindObjectsSortMode.None);

        blueTeamScores = new List<ResultScoreInfo>();
        orangeTeamScores = new List<ResultScoreInfo>();

        if (IsServer)
        {
            if (surrenderManager != null)
            {
                surrenderManager.onSurrenderVoteResult += OnTeamSurrendered;
            }
            gameTime.Value = 0f;
        }
        FINAL_STRETCH_TIME = currentMap.finalStretchTime;
        MAX_GAME_TIME = currentMap.gameTime;

        InitializePassiveExpQueue();

        StartCoroutine(HandlePassiveExp());
        StartCoroutine(HandleFarmLevelUps());
    }

    [Rpc(SendTo.Server)]
    public void NotifyPlayerLoadedMapRPC(ulong clientId, RpcParams rpcParams = default)
    {
        loadingBarrier.MarkReady(rpcParams.Receive.SenderClientId, clientId, ClientLoadStage.Map);
    }

    [Rpc(SendTo.Server)]
    public void NotifyPlayerLoadedPokemonsRPC(ulong clientId, RpcParams rpcParams = default)
    {
        loadingBarrier.MarkReady(rpcParams.Receive.SenderClientId, clientId, ClientLoadStage.Pokemon);
    }

    private void OnFinalStretchChanged(bool previous, bool current)
    {
        if (!current) return;
        AudioManager.StopAllMusic();
        AudioManager.PlayMusic(currentMap.finalStretchMusic, true);
        AudioManager.PlaySound(DefaultAudioSounds.Final_Stretch_Begin);
        AudioManager.PlaySound(DefaultAudioSounds.AnnouncerFinalStretch);
        onFinalStretch?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        contentLoadGeneration++;
        DetachNetworkEvents();
        StopAllCoroutines();
        gameState.OnValueChanged -= GameStateChanged;
        finalStretch.OnValueChanged -= OnFinalStretchChanged;
        if (surrenderManager != null) surrenderManager.onSurrenderVoteResult -= OnTeamSurrendered;
        if (startAnimationUI != null) startAnimationUI.OnAnimationComplete -= OnStartAnimationComplete;
    }

    private void GameStateChanged(GameState previous, GameState current)
    {
        onGameStateChanged?.Invoke(current);
        if (current == GameState.Starting)
        {
            LoadingScreen.Instance.HideMatchLoadingScreen();
        }
    }

    public IEnumerator StartGameRoutine()
    {
        if (IsServer)
        {
            gameState.Value = GameState.Starting;
        }
        yield break;
    }
    
    private void OnStartAnimationComplete()
    {
        if (IsServer)
        {
            gameState.Value = GameState.Playing;
        }
        
        // Unsubscribe to prevent memory leaks
        if (startAnimationUI != null)
        {
            startAnimationUI.OnAnimationComplete -= OnStartAnimationComplete;
        }
    }

    public void AddPlayer(PlayerManager player)
    {
        if (player == null || players.Contains(player)) return;
        players.Add(player);
        OnPlayerAdded?.Invoke(player);
    }

    public void RemovePlayer(PlayerManager player)
    {
        if (player == null || !players.Remove(player)) return;
        OnPlayerRemoved?.Invoke(player);
    }

    private void InitializePassiveExpQueue()
    {
        passiveExpQueue.Clear();
        currentPassiveExpSetting = null;

        PassiveExpSetting[] passiveExpSettings = currentMap.passiveExpSettings;
        if (passiveExpSettings == null || passiveExpSettings.Length == 0)
        {
            return;
        }

        PassiveExpSetting[] sortedSettings = (PassiveExpSetting[])passiveExpSettings.Clone();
        Array.Sort(sortedSettings, (left, right) => left.activationTime.CompareTo(right.activationTime));

        foreach (PassiveExpSetting setting in sortedSettings)
        {
            passiveExpQueue.Enqueue(setting);
        }

        if (passiveExpQueue.Count > 0)
        {
            currentPassiveExpSetting = passiveExpQueue.Dequeue();
        }
    }

    private void UpdatePassiveExpSetting()
    {
        while (passiveExpQueue.Count > 0 && gameTime.Value >= passiveExpQueue.Peek().activationTime)
        {
            currentPassiveExpSetting = passiveExpQueue.Dequeue();
        }
    }

    private IEnumerator HandlePassiveExp()
    {
        yield return new WaitUntil(() => gameState.Value == GameState.Playing);

        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (gameState.Value == GameState.Playing)
            {
                UpdatePassiveExpSetting();

                if (currentPassiveExpSetting != null)
                {
                    // A match-local roster, not delegates targeting scene-owned
                    // Pokemon that can already have been destroyed on teardown.
                    for (int i = players.Count - 1; i >= 0; i--)
                    {
                        var player = players[i];
                        if (player == null || player.Pokemon == null || !player.IsSpawned)
                        {
                            players.RemoveAt(i);
                            OnPlayerRemoved?.Invoke(player);
                            continue;
                        }
                        if (player.Pokemon.BaseStats != null) player.Pokemon.GainPassiveExp(currentPassiveExpSetting.value);
                    }
                    onUpdatePassiveExp?.Invoke(currentPassiveExpSetting.value);
                }
            }
        }
    }

    private IEnumerator HandleFarmLevelUps()
    {
        yield return new WaitUntil(() => gameState.Value == GameState.Playing);

        while (true)
        {
            yield return new WaitForSeconds(30f);
            if (gameState.Value == GameState.Playing)
            {
                onFarmLevelUps?.Invoke();
            }
        }
    }

    void Update()
    {
        if (gameState.Value == GameState.Playing)
        {
            if (IsServer)
            {
                gameTime.Value += Time.deltaTime;

                switch (currentMap.gameMode)
                {
                    case GameMode.Timed:
                        if (gameTime.Value >= MAX_GAME_TIME)
                        {
                            gameTime.Value = MAX_GAME_TIME;

                            EndGameRPC(GenerateGameResults());
                        }
                        else if (gameTime.Value >= FINAL_STRETCH_TIME && !finalStretch.Value)
                        {
                            finalStretch.Value = true;
                        }
                        break;
                    case GameMode.Timeless:
                        if (gameTime.Value >= FINAL_STRETCH_TIME && !finalStretch.Value)
                        {
                            finalStretch.Value = true;
                        }

                        if (blueTeamScore.Value >= currentMap.maxScore || orangeTeamScore.Value >= currentMap.maxScore)
                        {
                            EndGameRPC(GenerateGameResults());
                        }
                        break;
                    default:
                        break;
                }

                // Debug
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (Keyboard.current.oKey.wasPressedThisFrame)
                {
                    gameTime.Value = MAX_GAME_TIME;
                }
                if (Keyboard.current.iKey.wasPressedThisFrame)
                {
                    gameTime.Value = FINAL_STRETCH_TIME - 20f;
                }
                if (Keyboard.current.uKey.wasPressedThisFrame)
                {
                    gameTime.Value = 170f;
                }
#endif
            }
        }
    }

    private GameResults GenerateGameResults()     
    {
        List<PlayerStats> stats = new List<PlayerStats>();
        PlayerNetworkManager[] playerNetworkManagers = FindObjectsByType<PlayerNetworkManager>(FindObjectsSortMode.None);

        foreach (var playerNetworkManager in playerNetworkManagers)
        {
            stats.Add(playerNetworkManager.PlayerStats);
        }
        foreach (var player in players)
            if (player != null && player.IsBot) stats.Add(player.PlayerStats);

        return new GameResults
        {
            WinningTeam = GetWinningTeam(),
            BlueTeamScore = blueTeamScore.Value,
            OrangeTeamScore = orangeTeamScore.Value,
            Surrendered = false,
            TotalGameTime = gameTime.Value,
            BlueTeamScores = blueTeamScores.ToArray(),
            OrangeTeamScores = orangeTeamScores.ToArray(),
            PlayerStats = stats.ToArray()
        };
    }

    private Team GetWinningTeam()
    {
        return blueTeamScore.Value > orangeTeamScore.Value ? Team.Blue : Team.Orange;
    }

    [Rpc(SendTo.Server)]
    public void GoalScoredRpc(ScoreInfo info)
    {
        if (gameState.Value != GameState.Playing)
        {
            return;
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.scorerId].TryGetComponent(out Pokemon pokemon))
        {
            PlayerManager scorer = pokemon.GetComponent<PlayerManager>();
            RecordResultScore(pokemon.TeamMember.Team, info.scoredPoints, scorer != null ? scorer.LobbyPlayer.Id : string.Empty);

            if (pokemon.Type == PokemonType.Player)
                ShowGoalScoredRpc(info);

            if (pokemon.TeamMember.Team == Team.Orange)
            {
                orangeTeamScore.Value += info.scoredPoints;
            }
            else
            {
                blueTeamScore.Value += info.scoredPoints;
            }
        }
    }

    public void StartSurrenderVote()
    {
        if (GameState != GameState.Playing || surrenderManager == null)
        {
            return;
        }

        surrenderManager.StartSurrenderVoteRPC(LobbyController.Instance.GetLocalPlayerTeam());
        StartCoroutine(SurrenderCooldown());
    }

    private IEnumerator SurrenderCooldown()
    {
        surrenderButton.interactable = false;
        yield return new WaitForSeconds(35f);
        surrenderButton.interactable = true;
    }

    private void OnTeamSurrendered(Team orangeTeam, bool surrenderResults)
    {
        if (!surrenderResults)
        {
            return;
        }

        GameResults results = GenerateGameResults();
        results.Surrendered = true;
        results.WinningTeam = orangeTeam == Team.Orange ? Team.Blue : Team.Orange;

        EndGameRPC(results);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowGoalScoredRpc(ScoreInfo info)
    {
        PlayerManager scorer = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.scorerId].GetComponent<PlayerManager>();
        BattleUIManager.instance.ShowScore(info.scoredPoints, scorer.CurrentTeam.Team, scorer.Pokemon.Portrait);

        AudioManager.PlaySound(DefaultAudioSounds.Game_Ui_Score_Allies);

        if (info.scoredPoints >= 50)
        {
            AudioManager.PlaySound(DefaultAudioSounds.AnnouncerWhatAGoal);
        }
    }

    private void RecordResultScore(Team team, ushort scoredPoints, string playerID)
    {
        ResultScoreInfo score = new ResultScoreInfo(scoredPoints, playerID, gameTime.Value);
        if (team == Team.Orange)
        {
            orangeTeamScores.Add(score);
        }
        else
        {
            blueTeamScores.Add(score);
        }
    }

    public void EndGame()
    {
        if (!IsServer)
        {
            return;
        }

        GameResults results = GenerateGameResults();
        EndGameRPC(results);
    }

    [Rpc(SendTo.Everyone)]
    void EndGameRPC(GameResults gameResults)
    {
        LobbyController.Instance.GameResults = gameResults;
        AudioManager.PlaySound(DefaultAudioSounds.AnnouncerTImeUp);

        if (gameResults.Surrendered)
        {
            Team localTeam = LobbyController.Instance.GetLocalPlayerTeam();
            bool yourTeamSurrendered = localTeam != gameResults.WinningTeam;

            BattleUIManager.instance.ShowSurrenderTextbox(yourTeamSurrendered);
        }

        if (IsServer)
        {
            gameState.Value = GameState.Ended;
        }

        if (currentMap.overrideDefaultResultScreen) return;

        StartCoroutine(EndGameRoutine());
    }

    public IEnumerator EndGameRoutine()
    {
        yield return new WaitForSeconds(1.7f);

        LoadingScreen.Instance.ShowGenericLoadingScreen();
        AudioManager.StopAllMusic();

        yield return new WaitForSeconds(0.3f);

        LobbyController.Instance.ShouldLoadResultsScreen = true;

        if (IsServer)
        {
            LobbyController.Instance.LoadResultsScreen();
        }
    }

    public bool TryGetPlayerNetworkManager(ulong clientId, out PlayerNetworkManager playerNetworkManager)
    {
        playerNetworkManager = null;
        PlayerNetworkManager[] players = FindObjectsByType<PlayerNetworkManager>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.OwnerClientId == clientId)
            {
                playerNetworkManager = player;
                return true;
            }
        }
        return false;
    }

    public override void OnDestroy()
    {
        contentLoadGeneration++;
        DetachNetworkEvents();
        if (Instance == this) Instance = null;
        if (pokemonHandle.IsValid()) Addressables.Release(pokemonHandle);
        // Disconnects can destroy this object before either operation completes.
        // Never read Result on an invalid/failed operation or skip base cleanup.
        if (loadHandle.IsValid())
        {
            if (loadHandle.Status == AsyncOperationStatus.Succeeded && loadHandle.Result.Scene.isLoaded)
                Addressables.UnloadSceneAsync(loadHandle);
            else
                Addressables.Release(loadHandle);
        }
        base.OnDestroy();
    }

    public Vector3[] GetRotomPath(Team orangeTeam, int laneID)
    {
        foreach (var lane in lanes)
        {
            if (lane.Team == orangeTeam)
            {
                return lane.GetRotomPositions(laneID);
            }
        }

        return null;
    }
}
