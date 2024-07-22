using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class GoalZone : NetworkBehaviour
{
    [SerializeField] private int maxScore;
    [SerializeField] private bool orangeTeam;
    [SerializeField] private int healAmount;
    [SerializeField] private float shieldAmount;
    [SerializeField] private int goalTier;
    [SerializeField] private int goalLaneId;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private GameObject orangeModel, blueModel;
    [SerializeField] private VisionController visionController;

    [Space]
    // Used to trigger farm when goal is destroyed, use the normal event for c# code
    [SerializeField] private UnityEvent onGoalZoneDestroyedUnityEvent = new UnityEvent();

    private StatChange statChange = new StatChange(60, Stat.Speed, 0, false, true, true, 1);

    private NetworkVariable<bool> isActive = new NetworkVariable<bool>();

    private NetworkVariable<int> currentScore = new NetworkVariable<int>();

    public bool IsActive { get => isActive.Value; }
    public int CurrentScore { get => currentScore.Value; set => currentScore.Value = value; }
    public int MaxScore { get => maxScore; }
    public int GoalTier { get => goalTier; }
    public int GoalLaneId { get => goalLaneId; }
    public bool OrangeTeam { get => orangeTeam; }

    private List<PlayerManager> playerManagerList = new List<PlayerManager>();

    public event Action<int, int> onGoalZoneDestroyed;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartCoroutine(GoalZoneHealing());
        }
        scoreText.color = orangeTeam ? Color.yellow : Color.blue;
        Destroy(orangeTeam ? blueModel : orangeModel);
        scoreText.text = $"{maxScore - currentScore.Value}/{maxScore}";
        currentScore.OnValueChanged += UpdateGraphics;

        visionController.TeamToIgnore = orangeTeam;
        visionController.IsEnabled = orangeTeam == LobbyController.Instance.GetLocalPlayerTeam();
        visionController.transform.parent = null;

        MinimapManager.Instance.CreateGoalzoneIcon(this);

        if (goalTier == 0)
        {
            scoreText.gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            playerManagerList.Add(playerManager);
            if (IsActive)
            {
                if (playerManager.OrangeTeam != orangeTeam)
                {
                    playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Disabled);
                }
                playerManager.GoalZone = this;
            }
            //playerManager.CanScore = IsActive ? playerManager.OrangeTeam != orangeTeam : false;
            playerManager.onGoalScored += OnScore;
            if (playerManager.OrangeTeam == orangeTeam && IsServer)
            {
                playerManager.Pokemon.AddStatChange(statChange);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            playerManager.ScoreStatus.AddStatus(ActionStatusType.Disabled);
            playerManager.onGoalScored -= OnScore;
            if (playerManager.OrangeTeam == orangeTeam && IsServer)
            {
                playerManager.Pokemon.RemoveStatChangeWithIDRPC(1);
            }
            playerManager.GoalZone = null;
            playerManagerList.Remove(playerManager);
        }
    }

    public void GetAlliesInGoal(bool orangeTeam, out int alliesInGoal, out int enemiesInGoal)
    {
        alliesInGoal = -1;
        enemiesInGoal = 0;
        foreach (var player in playerManagerList)
        {
            if (player.OrangeTeam == orangeTeam)
            {
                alliesInGoal++;
            }
            else
            {
                enemiesInGoal++;
            }
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
                    if (!player.Pokemon.IsHPFull())
                    {
                        player.Pokemon.HealDamage(healAmount);
                    }
                    player.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(player.Pokemon.GetMaxHp() * shieldAmount), 1, 0, 1.5f, true));
                }
            }
            yield return new WaitForSeconds(1.5f);
        }
    }

    private void OnScore(int amount)
    {
        if (GameManager.Instance.FinalStretch)
        {
            amount *= 2;
        }

        if (IsServer) {
            currentScore.Value += amount;
        } else {
            SetCurrentScoreRPC(currentScore.Value + amount);
        }
    }

    [Rpc(SendTo.Server)]
    private void SetCurrentScoreRPC(int amount)
    {
        currentScore.Value = amount;
    }

    private void UpdateGraphics(int previous, int current)
    {
        scoreText.text = $"{maxScore - current}/{maxScore}";
        if (current >= maxScore)
        {
            DestroyGoalZone();
        }
    }

    private void DestroyGoalZone()
    {
        foreach (var player in playerManagerList)
        {
            player.onGoalScored -= OnScore;
            player.ScoreStatus.AddStatus(ActionStatusType.Disabled);
        }
        gameObject.SetActive(false);
        visionController.IsEnabled = false;
        onGoalZoneDestroyed?.Invoke(goalLaneId, goalTier);
        onGoalZoneDestroyedUnityEvent.Invoke();
    }

    public void SetIsActive(bool value)
    {
        if (IsServer)
        {
            isActive.Value = value;
        }
        else
        {
            SetIsActiveRPC(value);
        }
    }

    [Rpc(SendTo.Server)]
    private void SetIsActiveRPC(bool value)
    {
        isActive.Value = value;
    }
}
