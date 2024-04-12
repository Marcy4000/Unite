using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using Unity.Netcode;
using System.Linq;
using UnityEngine.SceneManagement;

public enum GameState
{
    Waiting,
    Playing,
    Ended
}

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [SerializeField] private TMP_Text timerText;
    private NetworkVariable<float> gameTime = new NetworkVariable<float>(600f);
    private NetworkVariable<int> blueTeamScore = new NetworkVariable<int>(0);
    private NetworkVariable<int> orangeTeamScore = new NetworkVariable<int>(0);

    private List<PlayerManager> players = new List<PlayerManager>();
    private GoalZone[] goalZones;

    public NetworkVariable<float> GameTime => gameTime;
    public int BlueTeamScore => blueTeamScore.Value;
    public int OrangeTeamScore => orangeTeamScore.Value;

    private NetworkVariable<GameState> gameState = new NetworkVariable<GameState>(GameState.Waiting);

    public event Action<int> onUpdatePassiveExp;
    public event Action<GameState> onGameStateChanged;

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
        if (sceneName.Equals("RemoatStadium"))
        {
            StartCoroutine(StartGameDelayed());
        }
    }

    private IEnumerator StartGameDelayed()
    {
        yield return new WaitForSeconds(2f);
        LoadingScreen.Instance.HideMatchLoadingScreen();
        StartGame();
    }

    public override void OnNetworkSpawn()
    {
        UpdateTimerText();

        gameState.OnValueChanged += GameStateChanged;
        goalZones = FindObjectsOfType<GoalZone>();

        StartCoroutine(HandlePassiveExp());
    }

    private void GameStateChanged(GameState previous, GameState current)
    {
        onGameStateChanged?.Invoke(current);
        if (current == GameState.Ended)
        {
            NetworkManagerUI.instance.DebugShowScore();
        }
    }

    public void StartGame()
    {
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
                    EndGame();
                }
            }
            UpdateTimerText();
        }
    }

    void UpdateTimerText()
    {
        int minutes = Mathf.FloorToInt(gameTime.Value / 60f);
        int seconds = Mathf.FloorToInt(gameTime.Value % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    [Rpc(SendTo.Server)]
    public void GoalScoredRpc(ScoreInfo info)
    {
        PlayerManager scorer = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.scorerId].GetComponent<PlayerManager>();
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
    }

    void EndGame()
    {
        gameState.Value = GameState.Ended;
        // Display end game UI, show winner, etc.
    }
}
