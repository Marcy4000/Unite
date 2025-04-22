using JSAM;
using System;
using System.Collections;
using System.Collections.Generic;
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

    public float MAX_GAME_TIME { get; private set; } = 600f;
    public float FINAL_STRETCH_TIME { get; private set; } = 120f;

    [SerializeField] private SurrenderManager surrenderManager;
    [SerializeField] private Button surrenderButton; //This shouldn't be here but eh

    private NetworkVariable<float> gameTime = new NetworkVariable<float>(600f);
    private NetworkVariable<ushort> blueTeamScore = new NetworkVariable<ushort>(0);
    private NetworkVariable<ushort> orangeTeamScore = new NetworkVariable<ushort>(0);

    private NetworkVariable<bool> finalStretch = new NetworkVariable<bool>(false);

    private Dictionary<ulong, bool> playerLoadedMap = new Dictionary<ulong, bool>();
    private Dictionary<ulong, bool> playerLoadedPokemons = new Dictionary<ulong, bool>();

    private List<PlayerManager> players = new List<PlayerManager>();
    private LaneManager[] lanes;

    private List<ResultScoreInfo> blueTeamScores;
    private List<ResultScoreInfo> orangeTeamScores;

    private MapInfo currentMap;

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
    public event Action onFinalStretch;
    public event Action onFarmLevelUps;

    private AsyncOperationHandle<SceneInstance> loadHandle;
    private AsyncOperationHandle<IList<PokemonBase>> pokemonHandle;

    private void Awake()
    {
        Instance = this;
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (!IsServer)
        {
            return;
        }

        players.RemoveAll(player => player.OwnerClientId == clientId);
        players.RemoveAll(player => player == null);
    }

    private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        MapInfo selectedMap = CharactersList.Instance.GetCurrentLobbyMap();
        if (sceneName.Equals(selectedMap.sceneName) && IsServer)
        {
            StartCoroutine(WaitForPlayersToLoad());
        }
    }

    private IEnumerator WaitForPlayersToLoad()
    {
        while (playerLoadedMap.ContainsValue(false) || playerLoadedPokemons.ContainsValue(false))
        {
            yield return null;
        }
        StartGameRPC();
    }

    private IEnumerator StartGameDelayed()
    {
        yield return new WaitForSeconds(0.1f);
        AudioManager.StopMusic(DefaultAudioMusic.LoadingTheme);
        AudioManager.PlaySound(DefaultAudioSounds.LoadComplete);
        if (IsServer)
        {
            gameState.Value = GameState.Initialising;
        }
        yield return new WaitForSeconds(1.9f);
        StartCoroutine(StartGameRoutine());
    }

    [Rpc(SendTo.Everyone)]
    private void StartGameRPC()
    {
        StartCoroutine(StartGameDelayed());
    }

    public override void OnNetworkSpawn()
    {
        currentMap = CharactersList.Instance.GetCurrentLobbyMap();

        loadHandle = Addressables.LoadSceneAsync(currentMap.mapSceneKey, LoadSceneMode.Additive);

        pokemonHandle = Addressables.LoadAssetsAsync<PokemonBase>("characters", null);

        loadHandle.Completed += (handle) =>
        {
            NotifyPlayerLoadedMapRPC(NetworkManager.Singleton.LocalClientId);
        };

        pokemonHandle.Completed += (handle) =>
        {
            NotifyPlayerLoadedPokemonsRPC(NetworkManager.Singleton.LocalClientId);
        };

        LoadingScreen.Instance.SetLoadingManagerOperation(loadHandle);

        gameState.OnValueChanged += GameStateChanged;
        finalStretch.OnValueChanged += (prev, curr) =>
        {
            if (curr)
            {
                AudioManager.StopAllMusic();
                AudioManager.PlayMusic(currentMap.finalStretchMusic, true);
                AudioManager.PlaySound(DefaultAudioSounds.Final_Stretch_Begin);
                AudioManager.PlaySound(DefaultAudioSounds.AnnouncerFinalStretch);
                onFinalStretch?.Invoke();
            }
        };
        lanes = FindObjectsOfType<LaneManager>();

        blueTeamScores = new List<ResultScoreInfo>();
        orangeTeamScores = new List<ResultScoreInfo>();

        if (IsServer)
        {
            playerLoadedMap.Clear();
            foreach (var player in NetworkManager.Singleton.ConnectedClientsList)
            {
                playerLoadedMap.Add(player.ClientId, false);
            }

            playerLoadedPokemons.Clear();
            foreach (var player in NetworkManager.Singleton.ConnectedClientsList)
            {
                playerLoadedPokemons.Add(player.ClientId, false);
            }

            if (surrenderManager != null)
            {
                surrenderManager.onSurrenderVoteResult += OnTeamSurrendered;
            }
            gameTime.Value = 0f;
        }
        FINAL_STRETCH_TIME = currentMap.finalStretchTime;
        MAX_GAME_TIME = currentMap.gameTime;

        StartCoroutine(HandlePassiveExp());
        StartCoroutine(HandleFarmLevelUps());
    }

    [Rpc(SendTo.Server)]
    public void NotifyPlayerLoadedMapRPC(ulong clientId)
    {
        playerLoadedMap[clientId] = true;
    }

    [Rpc(SendTo.Server)]
    public void NotifyPlayerLoadedPokemonsRPC(ulong clientId)
    {
        playerLoadedPokemons[clientId] = true;
    }

    private void GameStateChanged(GameState previous, GameState current)
    {
        onGameStateChanged?.Invoke(current);
    }

    public IEnumerator StartGameRoutine()
    {
        if (IsServer)
        {
            gameState.Value = GameState.Starting;
        }
        LoadingScreen.Instance.HideMatchLoadingScreen();
        AudioManager.PlaySound(DefaultAudioSounds.Game_ui_Rookie_Scoreboard_1);

        yield return new WaitForSeconds(0.2f);

        AudioManager.PlayMusic(currentMap.normalMusic, true);

        yield return new WaitForSeconds(0.4f);

        AudioManager.PlaySound(DefaultAudioSounds.AnnouncerReady);

        yield return new WaitForSeconds(1.6f);

        AudioManager.PlaySound(DefaultAudioSounds.Game_ui_Rookie_Scoreboard_Go);

        yield return new WaitForSeconds(0.9f);

        if (IsServer)
        {
            gameState.Value = GameState.Playing;
        }
    }

    public void AddPlayer(PlayerManager player)
    {
        players.Add(player);
        onUpdatePassiveExp += player.Pokemon.GainPassiveExp;
    }

    public void RemovePlayer(PlayerManager player)
    {
        players.Remove(player);
        onUpdatePassiveExp -= player.Pokemon.GainPassiveExp;
    }

    private IEnumerator HandlePassiveExp()
    {
        yield return new WaitUntil(() => gameState.Value == GameState.Playing);

        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (gameState.Value == GameState.Playing)
            {
                if (gameTime.Value < 480)
                {
                    onUpdatePassiveExp?.Invoke(4);
                }
                else
                {
                    onUpdatePassiveExp?.Invoke(6);
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
        PlayerNetworkManager[] playerNetworkManagers = FindObjectsOfType<PlayerNetworkManager>();

        foreach (var playerNetworkManager in playerNetworkManagers)
        {
            stats.Add(playerNetworkManager.PlayerStats);
        }

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

        if (scorer.CurrentTeam.IsOnSameTeam(Team.Orange))
        {
            orangeTeamScores.Add(new ResultScoreInfo(info.scoredPoints, scorer.LobbyPlayer.Id, gameTime.Value));
        }
        else
        {
            blueTeamScores.Add(new ResultScoreInfo(info.scoredPoints, scorer.LobbyPlayer.Id, gameTime.Value));
        }
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

        StartCoroutine(EndGameRoutine());
    }

    public IEnumerator EndGameRoutine()
    {
        if (IsServer)
        {
            gameState.Value = GameState.Ended;
        }

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
        PlayerNetworkManager[] players = FindObjectsOfType<PlayerNetworkManager>();
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
        if (!loadHandle.Result.Scene.isLoaded)
        {
            return;
        }
        Addressables.Release(pokemonHandle);
        Addressables.UnloadSceneAsync(loadHandle);
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
