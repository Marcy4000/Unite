using JSAM;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
                AudioManager.PlayMusic(DefaultAudioMusic.RemoatFinalStretch);
                onFinalStretch?.Invoke();
            }
        };
        lanes = FindObjectsOfType<LaneManager>();

        blueTeamScores = new List<ResultScoreInfo>();
        orangeTeamScores = new List<ResultScoreInfo>();

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

        AudioManager.PlayMusic(DefaultAudioMusic.RemoatStadium);

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
                if (Debug.isDebugBuild)
                {
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
                }
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
}
