using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class UniteRoyaleTrainer : NetworkBehaviour
{
    [SerializeField] private TrainerModel trainerModel;
    [SerializeField] private NavMeshAgent navMeshAgent;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private float followDistance = 3f;
    [SerializeField] private float stopDistance = 1.5f;
    [SerializeField] private float updatePositionInterval = 0.2f;

    private NetworkVariable<ulong> assignedPlayerID = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private PlayerNetworkManager assignedPlayer;
    private float nextUpdateTime;
    private bool isRunning = false;

    private void Awake()
    {
        if (navMeshAgent == null)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (navMeshAgent != null)
        {
            navMeshAgent.stoppingDistance = stopDistance;
        }

        assignedPlayerID.OnValueChanged += OnAssignedPlayerChanged;
        
        if (assignedPlayerID.Value != 0)
        {
            FindAndAssignPlayer(assignedPlayerID.Value);
        }
    }

    private void OnAssignedPlayerChanged(ulong previousValue, ulong newValue)
    {
        if (newValue != 0)
        {
            FindAndAssignPlayer(newValue);
        }
    }

    private void FindAndAssignPlayer(ulong playerID)
    {
        var players = FindObjectsByType<PlayerNetworkManager>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.NetworkObjectId == playerID)
            {
                assignedPlayer = player;
                trainerModel.InitializeClothes(PlayerClothesInfo.Deserialize(player.LocalPlayer.Data["ClothingInfo"].Value));
                nameText.text = player.LocalPlayer.Data["PlayerName"].Value;
                break;
            }
        }
    }

    private void Update()
    {
        if (!IsSpawned || assignedPlayer == null || navMeshAgent == null)
        {
            return;
        }

        // Update position periodically to reduce NavMesh calculations
        if (Time.time >= nextUpdateTime)
        {
            nextUpdateTime = Time.time + updatePositionInterval;
            UpdateFollowPosition();
        }

        // Update animation based on movement
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        if (trainerModel == null || trainerModel.ActiveAnimator == null || navMeshAgent == null)
        {
            return;
        }

        // Check if the agent is moving based on velocity
        bool shouldBeRunning = navMeshAgent.velocity.sqrMagnitude > 0.1f;

        if (shouldBeRunning && !isRunning)
        {
            trainerModel.ActiveAnimator.SetTrigger("startRun");
            isRunning = true;
        }
        else if (!shouldBeRunning && isRunning)
        {
            trainerModel.ActiveAnimator.SetTrigger("stopRun");
            isRunning = false;
        }
    }

    private void UpdateFollowPosition()
    {
        if (assignedPlayer == null || assignedPlayer.transform == null || assignedPlayer.Player == null)
        {
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, assignedPlayer.Player.transform.position);

        // Only move if we're too far from the player
        if (distanceToPlayer > followDistance)
        {
            navMeshAgent.SetDestination(assignedPlayer.Player.transform.position);
        }
        else if (distanceToPlayer <= stopDistance)
        {
            // Stop moving if we're close enough
            navMeshAgent.ResetPath();
        }
    }

    [Rpc(SendTo.Server)]
    public void AssignPlayerServerRpc(ulong playerID)
    {
        assignedPlayerID.Value = playerID;
    }

    public void SetAssignedPlayer(ulong playerID)
    {
        if (IsServer)
        {
            assignedPlayerID.Value = playerID;
        }
        else
        {
            AssignPlayerServerRpc(playerID);
        }
    }

    public override void OnDestroy()
    {
        assignedPlayerID.OnValueChanged -= OnAssignedPlayerChanged;
        base.OnDestroy();
    }
}
