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

    public const ushort GoalShieldId = 64990;
    public const ushort GoalSpeedId = 64991;
    private StatChange statChange = new StatChange(60, Stat.Speed, 0, false, true, true, GoalSpeedId);
    private float nextHeal;
    private readonly HashSet<PlayerManager> boostedPlayers = new();
    public bool ProvidesDefense => GoalStatus == GoalStatus.Active;

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
        nextHeal = Time.time;
        scoreText.color = GetTeamColor();
        DestroyUneededModels();
        scoreText.text = $"{maxScore - currentScore.Value}<color=\"white\">/{maxScore}";
        currentScore.OnValueChanged += UpdateGraphics;
        goalStatus.OnValueChanged += HandleGoalStatusChanged;
        isActive.OnValueChanged += HandleActivationChanged;

        visionController.CurrentTeam = team;
        visionController.IsEnabled = IsServer || team == LobbyController.Instance.GetLocalPlayerTeam();
        visionController.transform.parent = null;
        visionController.gameObject.SetActive(IsServer || team == LobbyController.Instance.GetLocalPlayerTeam());

        MinimapManager.Instance.CreateGoalzoneIcon(this);

        goalZoneShield.SetShieldColor(Team == Team.Orange ? orangeColor : blueColor);

        if (goalTier == 0)
        {
            scoreText.gameObject.SetActive(false);
        }
    }

    private void HandleGoalStatusChanged(GoalStatus previous, GoalStatus current)
    {
        RefreshDefense();
        onGoalStatusChanged?.Invoke(current);
    }

    private void HandleActivationChanged(bool previous, bool current) => RefreshDefense();

    private static bool Living(PlayerManager player) => player != null && player.IsSpawned &&
        player.Pokemon != null && player.Pokemon.IsSpawned && player.Pokemon.CurrentHp > 0;

    private void RemoveDefense(PlayerManager player, bool removeShield)
    {
        if (!boostedPlayers.Remove(player) || player == null || player.Pokemon == null || !player.Pokemon.IsSpawned || !IsServer) return;
        player.Pokemon.RemoveStatChangeWithIDRPC(GoalSpeedId);
        if (removeShield) player.Pokemon.RemoveShieldWithIDRPC(GoalShieldId);
    }

    private void RefreshDefense()
    {
        bool defended = false;
        foreach (var player in playerManagerList)
        {
            bool eligible = ProvidesDefense && Living(player) && player.CurrentTeam.IsOnSameTeam(team);
            defended |= eligible;
            if (!IsServer) continue;
            if (eligible)
            {
                if (boostedPlayers.Add(player)) player.Pokemon.AddStatChange(statChange);
            }
            else RemoveDefense(player, true);
        }
        if (goalZoneShield != null)
        {
            if (defended) goalZoneShield.ShowShield();
            else goalZoneShield.HideShield();
        }
    }

    private void RemoveOccupant(PlayerManager player)
    {
        playerManagerList.Remove(player);
        RemoveDefense(player, !Living(player) || !ProvidesDefense);
        if (player == null || player.GoalZone != this) return;
        player.GoalZone = null;
        if (player.IsSpawned) player.ScoreStatus.AddStatus(ActionStatusType.Disabled);
    }

    public override void OnNetworkDespawn()
    {
        currentScore.OnValueChanged -= UpdateGraphics;
        goalStatus.OnValueChanged -= HandleGoalStatusChanged;
        isActive.OnValueChanged -= HandleActivationChanged;
        ClearOccupants();
        // Goal vision is detached from the goal transform at spawn.
        if (visionController != null) Destroy(visionController.gameObject);
        base.OnNetworkDespawn();
    }

    private void ClearOccupants()
    {
        for (int i = playerManagerList.Count - 1; i >= 0; i--)
        {
            RemoveDefense(playerManagerList[i], true);
            RemoveOccupant(playerManagerList[i]);
        }
        boostedPlayers.Clear();
        if (goalZoneShield != null) goalZoneShield.HideShield();
    }

    private void OnDisable() => ClearOccupants();

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
        if (!IsSpawned) return;
        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            if (!Living(playerManager) || playerManagerList.Contains(playerManager)) return;
            playerManagerList.Add(playerManager);
            if (IsActive)
            {
                if (!playerManager.CurrentTeam.IsOnSameTeam(team))
                {
                    playerManager.ScoreStatus.RemoveStatus(ActionStatusType.Disabled);
                }
                playerManager.GoalZone = this;
            }
            RefreshDefense();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerManager playerManager = other.GetComponent<PlayerManager>();
            if (playerManager == null) return;
            RemoveOccupant(playerManager);
            RefreshDefense();
        }
    }

    public void GetAlliesInGoal(Team team, out int alliesInGoal, out int enemiesInGoal)
    {
        alliesInGoal = -1;
        enemiesInGoal = 0;
        foreach (var player in playerManagerList)
        {
            if (!Living(player)) continue;
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

    private void HealOccupants()
    {
        if (!ProvidesDefense) return;
        foreach (var player in playerManagerList)
        {
            if (Living(player) && player.CurrentTeam.IsOnSameTeam(team))
            {
                if (!player.Pokemon.IsHPFull())
                {
                    player.Pokemon.HealEnvironmentRPC(healAmount);
                }
                // Refresh, rather than accumulating several minutes of shield
                // duration when a defender has stood here for a long time.
                player.Pokemon.RemoveShieldWithIDRPC(GoalShieldId);
                int amount = Mathf.FloorToInt(player.Pokemon.GetMaxHp() * shieldAmount);
                player.Pokemon.AddShieldRPC(new ShieldInfo(amount, GoalShieldId, 0, 2.5f, true, (ushort)amount));
            }
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
        foreach (var player in playerManagerList) RemoveDefense(player, true);
        ClearOccupants();
        gameObject.SetActive(false);
        visionController.IsEnabled = false;
        onGoalZoneDestroyed?.Invoke(goalLaneId, goalTier);
        onGoalZoneDestroyedUnityEvent.Invoke();
        if (IsServer)
        {
            goalStatus.Value = GoalStatus.Destroyed;
            SpatialAudioManager.Instance.PlaySpatialSound(DefaultAudioSounds.Play_UI_InGame_TransitionDestroyed, transform.position, team, AudioAudibility.Everyone);
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
        if (!IsSpawned) return;
        for (int i = playerManagerList.Count - 1; i >= 0; i--)
            if (!Living(playerManagerList[i])) RemoveOccupant(playerManagerList[i]);
        RefreshDefense();
        if (!IsServer)
        {
            return;
        }
        if (Time.time >= nextHeal)
        {
            nextHeal = Time.time + 1.5f;
            HealOccupants();
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
