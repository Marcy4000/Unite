using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [SerializeField] private TMP_Text timerText;
    private float gameTime = 600f; // 10 minutes in seconds
    private int blueTeamScore = 0;
    private int orangeTeamScore = 0;

    private PlayerManager[] players;
    private GoalZone[] goalZones;

    public float GameTime => gameTime;
    public int BlueTeamScore => blueTeamScore;
    public int OrangeTeamScore => orangeTeamScore;

    private bool gameEnded = false;

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
        while (!gameEnded)
        {
            yield return new WaitForSeconds(1f);
            if (gameTime > 480)
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
        if (!gameEnded)
        {
            gameTime -= Time.deltaTime;
            if (gameTime <= 0f)
            {
                EndGame();
            }
            UpdateTimerText();
        }
    }

    void UpdateTimerText()
    {
        int minutes = Mathf.FloorToInt(gameTime / 60f);
        int seconds = Mathf.FloorToInt(gameTime % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void GoalScored(bool orangeTeam, int amount=1)
    {
        if (orangeTeam)
        {
            orangeTeamScore += amount;
        }
        else
        {
            blueTeamScore += amount;
        }
        BattleUIManager.instance.ShowScore(amount, orangeTeam);
    }

    void EndGame()
    {
        gameEnded = true;
        // Display end game UI, show winner, etc.
    }
}
