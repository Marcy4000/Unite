using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public enum GoalStatus : byte
{
    Active,
    Destroyed,
    Weakened
}

public class GoalZone : NetworkBehaviour
{
    [SerializeField] private int maxScore;
    [SerializeField] private Team team;
    [SerializeField] private int healAmount;
    [SerializeField] private float shieldAmount;
    [SerializeField] private int goalTier;
    [SerializeField] private int goalLaneId;
    [SerializeField] private bool allowOvercaps = true;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private GameObject[] areaModels;
    [SerializeField] private VisionController visionController;
    [SerializeField] private GoalZoneShield goalZoneShield;

    [Space]
    [SerializeField] [ColorUsage(true, true)] private Color orangeColor;
    [SerializeField] [ColorUsage(true, true)] private Color blueColor;

    [Space]
    // Used to trigger farm when goal is destroyed, use the normal event for c# code
    [SerializeField] private UnityEvent onGoalZoneDestroyedUnityEvent = new UnityEvent();

    private NetworkVariable<float> weakenTime = new NetworkVariable<float>();

    private StatChange statChange = new StatChange(60, Stat.Speed, 0, false, true, true, 1);

    private NetworkVariable<bool> isActive = new NetworkVariable<bool>();

    private NetworkVariable<int> currentScore = new NetworkVariable<int>();

    private NetworkVariable<GoalStatus> goalStatus = new NetworkVariable<GoalStatus>();

    public bool IsActive { get => isActive.Value; }
    public int CurrentScore { get => currentScore.Value; set => currentScore.Value = value; }
    public int MaxScore { get => maxScore; }
    public int GoalTier { get => goalTier; }
    public int GoalLaneId { get => goalLaneId; }
    public Team Team { get => team; }
    public GoalStatus GoalStatus { get => goalStatus.Value; }

    public float WeakenTime { get => weakenTime.Value; }

    private List<PlayerManager> playerManagerList = new List<PlayerManager>();

    public event Action<int, int> onGoalZoneDestroyed;
    public event Action<GoalStatus> onGoalStatusChanged;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartCoroutine(GoalZoneHealing());
        }
        scoreText.color = GetTeamColor();
        DestroyUneededModels();
        scoreText.text = $"{maxScore - currentScore.Value}<color=\"white\">/{maxScore}";
        currentScore.OnValueChanged += UpdateGraphics;
        goalStatus.OnValueChanged += (previous, current) => { onGoalStatusChanged?.Invoke(current); };

        visionController.TeamToIgnore = team;
        visionController.IsEnabled = team == LobbyController.Instance.GetLocalPlayerTeam();
        visionController.transform.parent = null;
        visionController.gameObject.SetActive(team == LobbyController.Instance.GetLocalPlayerTeam());

        MinimapManager.Instance.CreateGoalzoneIcon(this);

        goalZoneShield.SetShieldColor(Team == Team.Orange ? orangeColor : blueColor);

        if (goalTier == 0)
        {
            scoreText.gameObject.SetActive(false);
        }
    }

    private Color GetTeamColor()
    {
        switch (team)
        {
            case Team.Blue:
                return Color.cyan;
            case Team.Orange:
                return Color.yellow;
            default:
                return Color.white;
        }
    }

    private void DestroyUneededModels()
    {
        int modelToKeep = 0;

        switch (team)
        {
            case Team.Neutral:
                modelToKeep = 0;
                break;
            case Team.Blue:
                modelToKeep = 1;
                break;
            case Team.Orange:
                modelToKeep = 2;
                break;
            default:
                modelToKeep = 0;
                break;
        }

        for (int i = 0; i < areaModels.Length; i++)
        {
            if (i != modelToKeep)
            {
                Destroy(areaModels[i]);
            }
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
                if (!playerManager.CurrentTeam.IsOnSameTeam(team))
                {
                    playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Disabled);
                }
                playerManager.GoalZone = this;
            }
            //playerManager.CanScore = IsActive ? playerManager.OrangeTeam != orangeTeam : false;
            if (playerManager.CurrentTeam.IsOnSameTeam(team) && IsServer)
            {
                playerManager.Pokemon.AddStatChange(statChange);
            }

            GetAlliesInGoal(Team, out int alliesInGoal, out int enemiesInGoal);

            if (alliesInGoal >= 0)
            {
                goalZoneShield.ShowShield();
            }
            else
            {
                goalZoneShield.HideShield();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            playerManager.ScoreStatus.AddStatus(ActionStatusType.Disabled);
            if (playerManager.CurrentTeam.IsOnSameTeam(team) && IsServer)
            {
                playerManager.Pokemon.RemoveStatChangeWithIDRPC(1);
            }
            playerManager.GoalZone = null;
            playerManagerList.Remove(playerManager);

            GetAlliesInGoal(Team, out int alliesInGoal, out int enemiesInGoal);

            if (alliesInGoal >= 0)
            {
                goalZoneShield.ShowShield();
            }
            else
            {
                goalZoneShield.HideShield();
            }
        }
    }

    public void GetAlliesInGoal(Team team, out int alliesInGoal, out int enemiesInGoal)
    {
        alliesInGoal = -1;
        enemiesInGoal = 0;
        foreach (var player in playerManagerList)
        {
            if (player.CurrentTeam.IsOnSameTeam(team))
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
                if (player.CurrentTeam.IsOnSameTeam(team))
                {
                    if (!player.Pokemon.IsHPFull())
                    {
                        player.Pokemon.HealDamageRPC(healAmount);
                    }
                    player.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(player.Pokemon.GetMaxHp() * shieldAmount), 1, 0, 2.5f, true, (ushort)Mathf.FloorToInt(player.Pokemon.GetMaxHp() * shieldAmount)));
                }
            }
            yield return new WaitForSeconds(1.5f);
        }
    }

    public int ScorePoints(int amount, ulong scorerID)
    {
        // This causes problems if the player tries to score X amounts of points in an overcap protected goal during double scoring

        if (GameManager.Instance.FinalStretch && GameManager.Instance.CurrentMap.gameMode == GameMode.Timed)
        {
            amount *= 2;
        }

        if (!allowOvercaps && currentScore.Value + amount > maxScore)
        {
            amount = maxScore - currentScore.Value;
        }

        ScoreInfo score = new ScoreInfo((ushort)amount, scorerID);

        GameManager.Instance.GoalScoredRpc(score);

        if (IsServer) {
            currentScore.Value += amount;
        } else {
            SetCurrentScoreRPC(currentScore.Value + amount);
        }

        return amount;
    }

    [Rpc(SendTo.Server)]
    private void SetCurrentScoreRPC(int amount)
    {
        currentScore.Value = amount;
    }

    private void UpdateGraphics(int previous, int current)
    {
        scoreText.text = $"{maxScore - current}<color=\"white\">/{maxScore}";
        if (current >= maxScore)
        {
            DestroyGoalZone();
        }
    }

    private void DestroyGoalZone()
    {
        foreach (var player in playerManagerList)
        {
            player.ScoreStatus.AddStatus(ActionStatusType.Disabled);
        }
        gameObject.SetActive(false);
        visionController.IsEnabled = false;
        onGoalZoneDestroyed?.Invoke(goalLaneId, goalTier);
        onGoalZoneDestroyedUnityEvent.Invoke();
        if (IsServer)
        {
            goalStatus.Value = GoalStatus.Destroyed;
        }
    }

    [Rpc(SendTo.Server)]
    public void UpdateGoalStatusRPC(GoalStatus status)
    {
        if (goalStatus.Value == GoalStatus.Destroyed)
        {
            return;
        }
        goalStatus.Value = status;
    }

    [Rpc(SendTo.Server)]
    public void WeaknenGoalZoneRPC(float time)
    {
        weakenTime.Value = time;
        UpdateGoalStatusRPC(GoalStatus.Weakened);
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

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        if (weakenTime.Value > 0)
        {
            weakenTime.Value -= Time.deltaTime;
        }

        if (goalStatus.Value == GoalStatus.Weakened && weakenTime.Value <= 0)
        {
            UpdateGoalStatusRPC(GoalStatus.Active);
        }
    }
}
