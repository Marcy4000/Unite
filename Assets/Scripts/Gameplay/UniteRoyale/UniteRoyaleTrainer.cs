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
    [SerializeField] private float destinationVariation = 1f;

    private NetworkVariable<ulong> assignedPlayerID = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private PlayerManager assignedPlayer;
    private float nextUpdateTime;
    private bool isRunning = false;
    private Animator movementAnimator;
    private Vector3 spawnPosition;
    private Vector3 followOffset;
    private bool wasPlayerDead;
    private bool initialThrowPending;
    private Animator throwAnimator;
    private int throwStateHash;
    private string throwIdleState;

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
        spawnPosition = transform.position;
        
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
        var players = FindObjectsByType<PlayerManager>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player.NetworkObjectId == playerID)
            {
                if (assignedPlayer != null)
                {
                    assignedPlayer.OnRespawn -= OnPlayerRespawn;
                }
                FinishThrowAnimation();
                assignedPlayer = player;
                assignedPlayer.OnRespawn += OnPlayerRespawn;
                wasPlayerDead = player.PlayerState == PlayerState.Dead;
                initialThrowPending = !wasPlayerDead;
                ChooseFollowOffset();
                trainerModel.InitializeClothes(PlayerClothesInfo.Deserialize(player.LobbyPlayer.Data["ClothingInfo"].Value));
                nameText.text = player.LobbyPlayer.Data["PlayerName"].Value;
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

        UpdatePlayerState();
        if (UpdateThrowAnimation())
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

    private void UpdatePlayerState()
    {
        bool isPlayerDead = assignedPlayer.PlayerState == PlayerState.Dead;
        if (isPlayerDead != wasPlayerDead)
        {
            wasPlayerDead = isPlayerDead;
            nextUpdateTime = 0f;
            if (isPlayerDead)
            {
                initialThrowPending = false;
                FinishThrowAnimation();
            }
        }

        if (initialThrowPending && !isPlayerDead && trainerModel != null && trainerModel.IsInitialized)
        {
            initialThrowPending = !TryPlayThrowAnimation();
        }

    }

    private void UpdateAnimation()
    {
        if (navMeshAgent == null)
        {
            return;
        }

        // Check if the agent is moving based on velocity
        UpdateMovementAnimation(navMeshAgent.velocity.sqrMagnitude > 0.1f);
    }

    private void UpdateMovementAnimation(bool shouldBeRunning)
    {
        if (throwAnimator != null)
        {
            return;
        }

        var animator = trainerModel != null ? trainerModel.ActiveAnimator : null;
        if (animator == null || !animator.isActiveAndEnabled)
        {
            return;
        }

        // Clothes load asynchronously and can replace the active gender's Animator
        // after movement has started. The new Animator has not received our triggers.
        if (movementAnimator != animator)
        {
            movementAnimator = animator;
            animator.ResetTrigger("startRun");
            animator.ResetTrigger("stopRun");
            isRunning = false;
        }

        animator.SetBool("useAltIdle", true);

        if (shouldBeRunning && !isRunning)
        {
            // An unconsumed stop must not immediately cancel the next run.
            animator.ResetTrigger("stopRun");
            animator.SetTrigger("startRun");
            isRunning = true;
        }
        else if (!shouldBeRunning && isRunning)
        {
            animator.ResetTrigger("startRun");
            animator.SetTrigger("stopRun");
            isRunning = false;
        }
    }

    private void UpdateFollowPosition()
    {
        if (assignedPlayer == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return;
        }

        if (assignedPlayer.PlayerState == PlayerState.Dead)
        {
            navMeshAgent.SetDestination(spawnPosition);
            return;
        }

        Vector3 destination = assignedPlayer.transform.position + followOffset;
        if (!NavMesh.SamplePosition(destination, out var hit, destinationVariation, navMeshAgent.areaMask))
        {
            return;
        }
        destination = hit.position;
        float distanceToPlayer = Vector3.Distance(transform.position, destination);

        // Only move if we're too far from the player
        if (distanceToPlayer > followDistance)
        {
            navMeshAgent.SetDestination(destination);
        }
        else if (distanceToPlayer <= stopDistance)
        {
            // Stop moving if we're close enough
            navMeshAgent.ResetPath();
        }
    }

    private void ChooseFollowOffset()
    {
        Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(0.5f, 1f) * destinationVariation;
        followOffset = new Vector3(offset.x, 0f, offset.y);
    }

    private void OnPlayerRespawn()
    {
        initialThrowPending = false;
        nextUpdateTime = 0f;
        ChooseFollowOffset();
        TryPlayThrowAnimation();
    }

    private bool TryPlayThrowAnimation()
    {
        if (assignedPlayer == null || trainerModel == null || !trainerModel.IsInitialized)
        {
            return false;
        }

        var animator = trainerModel.ActiveAnimator;
        if (animator == null || !animator.isActiveAndEnabled || !animator.isInitialized)
        {
            return false;
        }

        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.ResetPath();
        }
        movementAnimator = animator;
        throwAnimator = animator;
        isRunning = false;
        animator.ResetTrigger("startRun");
        animator.ResetTrigger("stopRun");
        animator.SetBool("useAltIdle", true);
        throwStateHash = Animator.StringToHash(trainerModel.IsMale ? "ani_throwball_lob_male" : "ani_throwball_lob_female");
        throwIdleState = trainerModel.IsMale ? "ani_idleb_lob_male" : "ani_idleb_lob_female";
        animator.Play(throwStateHash, 0, 0f);
        animator.Update(0f);
        FacePlayer();
        return true;
    }

    private bool UpdateThrowAnimation()
    {
        if (throwAnimator == null)
        {
            return false;
        }

        var state = throwAnimator.GetCurrentAnimatorStateInfo(0);
        if (throwAnimator != trainerModel.ActiveAnimator || !throwAnimator.isActiveAndEnabled
            || state.shortNameHash != throwStateHash
            || (!throwAnimator.IsInTransition(0) && state.normalizedTime >= 1f))
        {
            FinishThrowAnimation();
            return false;
        }

        FacePlayer();
        return true;
    }

    private void FacePlayer()
    {
        Vector3 direction = assignedPlayer.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void FinishThrowAnimation()
    {
        if (throwAnimator == null)
        {
            return;
        }

        if (throwAnimator.isActiveAndEnabled
            && throwAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash == throwStateHash)
        {
            throwAnimator.Play(throwIdleState, 0, 0f);
        }
        throwAnimator = null;
        nextUpdateTime = 0f;
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
        if (assignedPlayer != null)
        {
            assignedPlayer.OnRespawn -= OnPlayerRespawn;
        }
        base.OnDestroy();
    }
}
