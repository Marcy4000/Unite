using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GoalZone : MonoBehaviour
{
    [SerializeField] private int maxScore;
    [SerializeField] private bool orangeTeam;
    [SerializeField] private int goalTier;
    [SerializeField] private TMP_Text scoreText;
    private bool isActive;
    private int currentScore;

    public bool IsActive { get => isActive; set => isActive = value; }
    public int CurrentScore { get => currentScore; set => currentScore = value; }
    public int MaxScore { get => maxScore; }
    public int GoalTier { get => goalTier; }
    public bool OrangeTeam { get => orangeTeam; }

    private List<PlayerManager> playerManagerList = new List<PlayerManager>();

    private void Start()
    {
        StartCoroutine(GoalZoneHealing());
        scoreText.color = orangeTeam ? Color.yellow : Color.blue;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            playerManagerList.Add(playerManager);
            playerManager.CanScore = playerManager.OrangeTeam != orangeTeam;
            playerManager.onGoalScored += OnScore;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            playerManager.CanScore = false;
            playerManager.onGoalScored -= OnScore;
            playerManagerList.Remove(playerManager);
        }
    }

    private IEnumerator GoalZoneHealing()
    {
        while (true)
        {
            foreach (var player in playerManagerList)
            {
                if (player.OrangeTeam == orangeTeam)
                {
                    player.Pokemon.HealDamage(200);
                }
            }
            yield return new WaitForSeconds(1.5f);
        }
    }

    private void OnScore(int amount)
    {
        currentScore += amount;
        scoreText.text = $"{maxScore-currentScore}/{maxScore}";
        if (currentScore >= maxScore)
        {
            DestroyGoalZone();
        }
    }

    private void DestroyGoalZone()
    {
        gameObject.SetActive(false);
    }
}
