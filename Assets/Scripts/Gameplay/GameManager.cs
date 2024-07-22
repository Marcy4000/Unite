using JSAM;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
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

    private const float MAX_GAME_TIME = 600f;
    private const float FINAL_STRETCH_TIME = 120f;

    [SerializeField] private SurrenderManager surrenderManager;
    [SerializeField] private Button surrenderButton; //This shouldn't be here but eh

    private NetworkVariable<float> gameTime = new NetworkVariable<float>(MAX_GAME_TIME);
    private NetworkVariable<ushort> blueTeamScore = new NetworkVariable<ushort>(0);
    private NetworkVariable<ushort> orangeTeamScore = new NetworkVariable<ushort>(0);

    private NetworkVariable<bool> finalStretch = new NetworkVariable<bool>(false);

    private List<PlayerManager> players = new List<PlayerManager>();
    private LaneManager[] lanes;

    private List<ResultScoreInfo> blueTeamScores;
    private List<ResultScoreInfo> orangeTeamScores;
    
    public float GameTime => gameTime.Value;
    public int BlueTeamScore => blueTeamScore.Value;
    public int OrangeTeamScore => orangeTeamScore.Value;
    public bool FinalStretch => finalStretch.Value;

    public List<PlayerManager> Players => players;

    private NetworkVariable<GameState> gameState = new NetworkVariable<GameState>(GameState.Waiting);

    public GameState GameState => gameState.Value;

    public event Action<int> onUpdatePassiveExp;
    public event Action<GameState> onGameStateChanged;
    public event Action onFinalStretch;

    private void Awake()
    {
        Instance = this;
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        string selectedMap = LobbyController.Instance.Lobby.Data["SelectedMap"].Value;
        if (sceneName.Equals(selectedMap))
        {
            StartCoroutine(StartGameDelayed());
        }
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

    public override void OnNetworkSpawn()
    {
        gameState.OnValueChanged += GameStateChanged;
        finalStretch.OnValueChanged += (prev, curr) =>
        {
            if (curr)
            {
                AudioManager.StopMusic(DefaultAudioMusic.RemoatStadium);
                AudioManager.PlayMusic(DefaultAudioMusic.RemoatFinalStretch, true);
                onFinalStretch?.Invoke();
            }
        };
        lanes = FindObjectsOfType<LaneManager>();

        blueTeamScores = new List<ResultScoreInfo>();
        orangeTeamScores = new List<ResultScoreInfo>();

        if (IsServer && surrenderManager != null)
        {
            surrenderManager.onSurrenderVoteResult += OnTeamSurrendered;
        }

        StartCoroutine(HandlePassiveExp());
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

        yield return new WaitForSeconds(0.2f);

        AudioManager.PlayMusic(DefaultAudioMusic.RemoatStadium, true);

        yield return new WaitForSeconds(3.1f);

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
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (gameState.Value == GameState.Playing)
            {
                if (gameTime.Value > 480)
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

    void Update()
    {
        if (gameState.Value == GameState.Playing)
        {
            if (IsServer)
            {
                gameTime.Value -= Time.deltaTime;

                if (gameTime.Value <= 0f)
                {
                    gameTime.Value = 0f;

                    EndGameRPC(GenerateGameResults());
                }
                else if (gameTime.Value <= FINAL_STRETCH_TIME && !finalStretch.Value)
                {
                    finalStretch.Value = true;
                }

                // Debug
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                if (Keyboard.current.oKey.wasPressedThisFrame)
                {
                    gameTime.Value = 0f;
                }
                if (Keyboard.current.iKey.wasPressedThisFrame)
                {
                    gameTime.Value = 140f;
                }
                if (Keyboard.current.uKey.wasPressedThisFrame)
                {
                    gameTime.Value = 430f;
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
            BlueTeamWon = blueTeamScore.Value > orangeTeamScore.Value,
            BlueTeamScore = blueTeamScore.Value,
            Surrendered = false,
            OrangeTeamScore = orangeTeamScore.Value,
            TotalGameTime = MAX_GAME_TIME - gameTime.Value,
            BlueTeamScores = blueTeamScores.ToArray(),
            OrangeTeamScores = orangeTeamScores.ToArray(),
            PlayerStats = stats.ToArray()
        };
    }

    [Rpc(SendTo.Server)]
    public void GoalScoredRpc(ScoreInfo info)
    {
        if (gameState.Value != GameState.Playing)
        {
            return;
        }

        PlayerManager scorer = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.scorerId].GetComponent<PlayerManager>();

        if (FinalStretch)
        {
            info.scoredPoints = (ushort)Mathf.FloorToInt(info.scoredPoints * 2f);
        }

        if (scorer.OrangeTeam)
        {
            orangeTeamScore.Value += info.scoredPoints;
        }
        else
        {
            blueTeamScore.Value += info.scoredPoints;
        }
        ShowGoalScoredRpc(info);
    }

    public void StartSurrenderVote()
    {
        if (GameState != GameState.Playing || surrenderManager == null)
        {
            return;
        }

        // TODO: implement an anctual surrender vote
        surrenderManager.StartSurrenderVoteRPC(LobbyController.Instance.GetLocalPlayerTeam());
        StartCoroutine(SurrenderCooldown());
    }

    private IEnumerator SurrenderCooldown()
    {
        surrenderButton.interactable = false;
        yield return new WaitForSeconds(35f);
        surrenderButton.interactable = true;
    }

    private void OnTeamSurrendered(bool orangeTeam, bool surrenderResults)
    {
        if (!surrenderResults)
        {
            return;
        }

        GameResults results = GenerateGameResults();
        results.Surrendered = true;
        results.BlueTeamWon = orangeTeam;

        EndGameRPC(results);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowGoalScoredRpc(ScoreInfo info)
    {
        PlayerManager scorer = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.scorerId].GetComponent<PlayerManager>();
        BattleUIManager.instance.ShowScore(info.scoredPoints, scorer.OrangeTeam, scorer.Pokemon.Portrait);
        if (scorer.OrangeTeam)
        {
            orangeTeamScores.Add(new ResultScoreInfo(info.scoredPoints, scorer.LobbyPlayer.Id, gameTime.Value));
        }
        else
        {
            blueTeamScores.Add(new ResultScoreInfo(info.scoredPoints, scorer.LobbyPlayer.Id, gameTime.Value));
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void EndGameRPC(GameResults gameResults)
    {
        LobbyController.Instance.GameResults = gameResults;

        if (gameResults.Surrendered)
        {
            bool localTeam = LobbyController.Instance.GetLocalPlayerTeam();
            bool yourTeamSurrendered = localTeam == gameResults.BlueTeamWon;

            BattleUIManager.instance.ShowSurrenderTextbox(yourTeamSurrendered);
        }

        StartCoroutine(EndGameRoutine());
    }

    private IEnumerator EndGameRoutine()
    {
        if (IsServer)
        {
            gameState.Value = GameState.Ended;
        }

        yield return new WaitForSeconds(1.7f);

        LoadingScreen.Instance.ShowGenericLoadingScreen();
        AudioManager.StopAllMusic();

        yield return new WaitForSeconds(0.3f);

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
}
