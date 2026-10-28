using DG.Tweening;
using JSAM;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BigJumpPad : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Animator jumpPadAnimator;
    [SerializeField] private GameObject landingIndicator; // Main indicator containing all positions
    [SerializeField] private Transform[] lanes; // Array of lane parents, each containing landing positions
    [SerializeField] private int[] laneAnimations;
    [SerializeField] private Transform lineObject; // Origin point of the jump pad
    [SerializeField] private string landingPositionTag = "JumpLandingPosition"; // Tag for landing position colliders

    [Header("Settings")]
    [SerializeField] private float jumpTime = 1f;
    [SerializeField] private float jumpHeight = 5f;
    [SerializeField] private float spawnTime;
    [SerializeField] private float despawnTime;
    [SerializeField] private bool flipInput = false;

    private NetworkVariable<bool> isActive = new NetworkVariable<bool>(true);

    private bool isJumping = false;
    private PlayerManager currentJumper;
    private readonly HashSet<Collider> localOverlaps = new();
    private readonly List<IDisposable> actionRestrictions = new();
    private readonly HashSet<ulong> serverJumpers = new();
    private Tween jumpTween;
    private Coroutine jumpCoroutine;
    private bool movementRestricted;
    private CameraController jumpCamera;

    private string[] jumpAnimations = new string[] { "JumpForward", "JumpLeft", "JumpRight" };

    private PlayerControls playerControls => InputManager.Instance.Controls;

    // Selection variables
    private bool inJumpSelection = false; // Whether the player is in jump selection state
    private int currentLane = 0;
    private int currentPosition = 0;
    private float selectionCooldown = 0.2f; // Cooldown time between selection changes
    private float selectionTimer = 0f;

    // Events for UI communication
    public static event Action<BigJumpPad> OnEnterJumpSelection;
    public static event Action OnExitJumpSelection;
    public static event Action<BigJumpPad> OnEnterJumpPad; // When player enters pad area
    public static event Action OnExitJumpPad; // When player exits pad area

    // Public properties for mobile input handler
    public bool InJumpSelection => inJumpSelection;
    public Transform[] Lanes => lanes;
    public PlayerManager CurrentJumper => currentJumper;

    public override void OnNetworkSpawn()
    {
        isActive.OnValueChanged += SetJumpPadActive;

        ResetIndicators();

        SetJumpPadActive(false, isActive.Value);

        if (!IsServer)
        {
            return;
        }

        if (spawnTime > 0)
        {
            isActive.Value = false;
        }
    }

    private void SetJumpPadActive(bool oldValue, bool active)
    {
        if (!active) CleanupPad();
        if (jumpPadAnimator != null) jumpPadAnimator.gameObject.SetActive(active);
    }

    private void OnDisable() => CleanupPad();

    private void CleanupPad()
    {
        ReleaseLocalJumper();
        StopAllCoroutines();
        serverJumpers.Clear();
    }

    public override void OnNetworkDespawn()
    {
        isActive.OnValueChanged -= SetJumpPadActive;
        CleanupPad();
        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (currentJumper != null && (!currentJumper.IsSpawned || !currentJumper.IsLocalHuman || currentJumper.Pokemon.CurrentHp <= 0))
            ReleaseLocalJumper();

        if (currentJumper != null && currentJumper.IsLocalHuman && !isJumping)
        {
            HandleJumpSelectionState();
        }

        if (!IsServer)
        {
            return;
        }

        if (despawnTime > 0f && GameManager.Instance.GameTime >= despawnTime)
        {
            isActive.Value = false;
            return;
        }

        if (GameManager.Instance.GameTime >= spawnTime && spawnTime > 0)
        {
            isActive.Value = true;
        }
    }

    private void HandleJumpSelectionState()
    {
        // Both desktop and mobile use the same input (Recall button/jump button on UI)
        if (!inJumpSelection)
        {
            if (playerControls.Movement.Recall.WasPressedThisFrame())
            {
                EnterJumpSelection();
            }
        }
        else
        {
#if !UNITY_ANDROID
            // Desktop: use joystick for selection
            HandleIndicatorSelection();
#endif
            // Both platforms: MoveB confirms the jump
            if (playerControls.Movement.MoveB.WasPressedThisFrame())
            {
                PerformJumpServerRPC(currentJumper.NetworkObjectId, currentLane, currentPosition);
            }
        }
    }

    private void EnterJumpSelection()
    {
        if (inJumpSelection || isJumping || !isActive.Value || currentJumper == null || !currentJumper.IsLocalHuman) return;

        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_InGame_BigJump_Ready);

        inJumpSelection = true;
        landingIndicator.SetActive(true);
        lineObject.gameObject.SetActive(true);
        UpdateIndicatorPosition();

        jumpCamera = CameraController.Instance;
        jumpCamera?.SetZoomOverride(this, new Vector3(0, 57.7f, -61), 30f, 100);

        OnEnterJumpSelection?.Invoke(this);
    }

    private void ExitJumpSelection()
    {
        if (!inJumpSelection) return; // Prevent multiple exits

        inJumpSelection = false;
        ResetIndicators();

        jumpCamera?.ReleaseZoomOverride(this);
        jumpCamera = null;

        OnExitJumpSelection?.Invoke();
    }

    private void ResetIndicators()
    {
        if (landingIndicator != null) landingIndicator.SetActive(false);
        if (lineObject != null) lineObject.gameObject.SetActive(false);
        currentLane = 0;
        currentPosition = 0;
        selectionTimer = 0;
    }

    private void ReleaseLocalJumper()
    {
        var player = currentJumper;
        currentJumper = null;
        if (player != null && player.Pokemon != null) player.Pokemon.OnDeath -= OnJumperDeath;
        if (jumpCoroutine != null) StopCoroutine(jumpCoroutine);
        jumpCoroutine = null;
        jumpTween?.Kill();
        jumpTween = null;
        if (movementRestricted && player != null) player.PlayerMovement.RemoveMovementRestriction();
        movementRestricted = false;
        isJumping = false;
        ReleaseActionRestrictions();
        ExitJumpSelection();
        ResetIndicators();
        bool wasOnPad = localOverlaps.Count > 0;
        localOverlaps.Clear();
        if (wasOnPad) OnExitJumpPad?.Invoke();
    }

    private void OnJumperDeath(DamageInfo damage) => ReleaseLocalJumper();

    private void ReleaseActionRestrictions()
    {
        foreach (var restriction in actionRestrictions) restriction.Dispose();
        actionRestrictions.Clear();
    }

    /// <summary>
    /// Called by mobile input handler when a landing position is tapped.
    /// Finds the lane and position indices for the tapped transform and performs the jump.
    /// </summary>
    public void OnMobileTapLandingPosition(Transform tappedPosition)
    {
        if (!inJumpSelection || isJumping || currentJumper == null || !currentJumper.IsLocalHuman) return;

        // Find the lane and position for the tapped transform
        for (int laneIndex = 0; laneIndex < lanes.Length; laneIndex++)
        {
            Transform lane = lanes[laneIndex];
            for (int posIndex = 0; posIndex < lane.childCount; posIndex++)
            {
                if (lane.GetChild(posIndex) == tappedPosition)
                {
                    // Update visual selection before jumping
                    currentLane = laneIndex;
                    currentPosition = posIndex;
                    UpdateIndicatorPosition();

                    // Perform the jump
                    PerformJumpServerRPC(currentJumper.NetworkObjectId, laneIndex, posIndex);
                    return;
                }
            }
        }

        Debug.LogWarning($"Tapped position {tappedPosition.name} not found in any lane");
    }

    /// <summary>
    /// Gets the tag used by landing position colliders for mobile raycast detection.
    /// </summary>
    public string GetLandingPositionTag() => landingPositionTag;

    private void HandleIndicatorSelection()
    {
        selectionTimer -= Time.deltaTime;
        if (selectionTimer > 0f) return;

        Vector2 movementInput = playerControls.Movement.AimMove.ReadValue<Vector2>();

        if (flipInput)
        {
            movementInput.x *= -1;
            movementInput.y *= -1;
        }

        if (movementInput.x < -0.1f)
        {
            currentPosition = Mathf.Max(currentPosition - 1, 0);
            selectionTimer = selectionCooldown;
        }
        else if (movementInput.x > 0.1f)
        {
            currentPosition = Mathf.Min(currentPosition + 1, lanes[currentLane].childCount - 1);
            selectionTimer = selectionCooldown;
        }
        if (movementInput.y > 0.1f)
        {
            currentLane = Mathf.Max(currentLane - 1, 0);
            currentPosition = Mathf.Clamp(currentPosition, 0, lanes[currentLane].childCount - 1);
            selectionTimer = selectionCooldown;
        }
        else if (movementInput.y < -0.1f)
        {
            currentLane = Mathf.Min(currentLane + 1, lanes.Length - 1);
            currentPosition = Mathf.Clamp(currentPosition, 0, lanes[currentLane].childCount - 1);
            selectionTimer = selectionCooldown;
        }

        UpdateIndicatorPosition();
    }

    private void UpdateIndicatorPosition()
    {
        foreach (Transform lane in lanes)
        {
            foreach (Transform position in lane)
            {
                position.localScale = Vector3.one; // Reset size of all indicators
            }
        }

        Transform targetPosition = lanes[currentLane].GetChild(currentPosition);
        targetPosition.localScale = Vector3.one * 1.2f; // Highlight the selected position

        UpdateLineIndicator(targetPosition);
    }

    private void UpdateLineIndicator(Transform target)
    {
        if (target == null) return;

        // Calculate the direction and distance
        Vector3 direction = target.position - lineObject.position;
        float distance = direction.magnitude;

        // Update the line's rotation and scale
        lineObject.forward = direction.normalized;
        float zScale = distance / 0.6f; // Convert distance to Z scale
        lineObject.localScale = new Vector3(lineObject.localScale.x, 7, zScale);
    }

    [Rpc(SendTo.Server)]
    private void PerformJumpServerRPC(ulong playerID, int lane, int position, RpcParams rpcParams = default)
    {
        if (!isActiveAndEnabled || !isActive.Value || serverJumpers.Contains(playerID)
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerID, out var playerObject)
            || !playerObject.TryGetComponent(out PlayerManager player)
            || player.IsBot || player.OwnerClientId != rpcParams.Receive.SenderClientId || player.Pokemon.CurrentHp <= 0
            || lane < 0 || lane >= lanes.Length || lanes[lane] == null
            || position < 0 || position >= lanes[lane].childCount) return;
        serverJumpers.Add(playerID);
        StartCoroutine(PerformJumpServer(player, lane, position));
    }

    private IEnumerator PerformJumpServer(PlayerManager player, int lane = 0, int position = 0)
    {
        ulong playerID = player.NetworkObjectId;
        Transform targetPosition = lanes[lane].GetChild(position);

        jumpPadAnimator.SetTrigger(GetJumpAnimation(lane));
        player.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, jumpTime, true, 0));

        StartJumpForPlayerRPC(player.NetworkObjectId, targetPosition.position, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));

        yield return new WaitForSeconds(jumpTime);

        serverJumpers.Remove(playerID);
        if (player == null || !player.IsSpawned || player.Pokemon.CurrentHp <= 0) yield break;

        GameObject[] enemiesHit = Aim.Instance.AimInCircleAtPosition(targetPosition.transform.position, 1f, AimTarget.NonAlly, player.CurrentTeam);

        foreach (var enemy in enemiesHit)
        {
            if (enemy.TryGetComponent(out Pokemon pokemon))
            {
                pokemon.ApplyKnockupRPC(1f, 0.65f);
                pokemon.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.65f, true, 0));
            }
        }
    }

    private string GetJumpAnimation(int lane)
    {
        int selectedAnimation = 0;

        try
        {
            selectedAnimation = laneAnimations[lane];
        }
        catch (System.Exception)
        {
            Debug.LogError("Lane index out of bounds");
        }

        return jumpAnimations[selectedAnimation];
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void StartJumpForPlayerRPC(ulong playerID, Vector3 targetPosition, RpcParams rpcParams = default)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerID, out var playerObject)
            && playerObject.TryGetComponent(out PlayerManager jumper) && jumper == currentJumper && jumper.IsLocalHuman && !isJumping)
        {
            jumpCoroutine = StartCoroutine(PerformJump(jumper, targetPosition));
        }
    }

    private IEnumerator PerformJump(PlayerManager player, Vector3 targetPosition)
    {
        isJumping = true;
        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_InGame_BigJump_Fly);
        player.PlayerMovement.AddMovementRestriction();
        movementRestricted = true;
        jumpTween = player.transform.DOJump(targetPosition, jumpHeight, 1, jumpTime);

        yield return new WaitForSeconds(jumpTime);

        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_InGame_BigJump_Done);
        jumpCoroutine = null;
        ReleaseLocalJumper();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || !isActive.Value || isJumping || !other.CompareTag("Player"))
        {
            return;
        }

        if (!other.TryGetComponent(out PlayerManager playerManager))
        {
            return;
        }

        if (playerManager.IsLocalHuman && (currentJumper == null || currentJumper == playerManager))
        {
            if (!localOverlaps.Add(other) || currentJumper == playerManager) return;
            currentJumper = playerManager;
            playerManager.Pokemon.OnDeath += OnJumperDeath;
            LockPlayerActions(playerManager);
            OnEnterJumpPad?.Invoke(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (!other.TryGetComponent(out PlayerManager playerManager))
        {
            return;
        }

        if (currentJumper == playerManager && localOverlaps.Remove(other) && localOverlaps.Count == 0)
        {
            if (isJumping)
            {
                OnExitJumpPad?.Invoke();
                ExitJumpSelection();
            }
            else
            {
                ReleaseLocalJumper();
                OnExitJumpPad?.Invoke();
            }
        }
    }

    private void LockPlayerActions(PlayerManager player)
    {
        actionRestrictions.Add(player.MovesController.AcquireMoveStatus(0, ActionStatusType.Busy));
        actionRestrictions.Add(player.MovesController.AcquireMoveStatus(1, ActionStatusType.Busy));
        actionRestrictions.Add(player.MovesController.AcquireMoveStatus(2, ActionStatusType.Busy));
        actionRestrictions.Add(player.MovesController.BattleItemStatus.AcquireStatus(ActionStatusType.Busy));
        actionRestrictions.Add(player.MovesController.BasicAttackStatus.AcquireStatus(ActionStatusType.Busy));
        actionRestrictions.Add(player.RecallStatus.AcquireStatus(ActionStatusType.Busy));
        actionRestrictions.Add(player.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
    }
}
