using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using Unity.Netcode;

public enum GameState
{
    Waiting,
    Playing,
    Ended
}

public class GameManager : NetworkBehaviour
{
    public static GameManager instance;

    [SerializeField] private TMP_Text timerText;
    private NetworkVariable<float> gameTime = new NetworkVariable<float>(600f);
    private NetworkVariable<int> blueTeamScore = new NetworkVariable<int>(0);
    private NetworkVariable<int> orangeTeamScore = new NetworkVariable<int>(0);

    private PlayerManager[] players;
    private GoalZone[] goalZones;

    public NetworkVariable<float> GameTime => gameTime;
    public int BlueTeamScore => blueTeamScore.Value;
    public int OrangeTeamScore => orangeTeamScore.Value;

    private GameState gameState = GameState.Playing;

    public event Action<int> onUpdatePassiveExp;

    private void Awake()
    {
        instance = this;
    }

    void Start()
    {
        UpdateTimerText();

        goalZones = FindObjectsOfType<GoalZone>();

        players = FindObjectsOfType<PlayerManager>();
        foreach (var player in players)
        {
            if (player.Pokemon.Type != PokemonType.Player)
                continue;

            onUpdatePassiveExp += player.Pokemon.GainPassiveExp;
        }

        StartCoroutine(HandlePassiveExp());
    }

    private IEnumerator HandlePassiveExp()
    {
        while (gameState == GameState.Playing)
        {
            yield return new WaitForSeconds(1f);
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

    void Update()
    {
        if (gameState == GameState.Playing)
        {
            if (IsServer)
            {
                gameTime.Value -= Time.deltaTime;
            }
            if (gameTime.Value <= 0f)
            {
                EndGame();
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
    public void GoalScoredRpc(bool orangeTeam, int amount=1)
    {
        if (orangeTeam)
        {
            orangeTeamScore.Value += amount;
        }
        else
        {
            blueTeamScore.Value += amount;
        }
        ShowGoalScoredRpc(amount, orangeTeam);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowGoalScoredRpc(int amount, bool orangeTeam)
    {
        BattleUIManager.instance.ShowScore(amount, orangeTeam);
    }

    void EndGame()
    {
        gameState = GameState.Ended;
        // Display end game UI, show winner, etc.
    }
}
