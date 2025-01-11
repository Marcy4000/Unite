using DG.Tweening;
using JSAM;
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

    [Header("Settings")]
    [SerializeField] private float jumpTime = 1f;
    [SerializeField] private float jumpHeight = 5f;
    [SerializeField] private float spawnTime;
    [SerializeField] private float despawnTime;
    [SerializeField] private bool flipInput = false;

    private NetworkVariable<bool> isActive = new NetworkVariable<bool>(true);

    private bool isJumping = false;
    private PlayerManager currentJumper;

    private string[] jumpAnimations = new string[] { "JumpForward", "JumpLeft", "JumpRight" };

    private PlayerControls playerControls => InputManager.Instance.Controls;

    // Selection variables
    private bool inJumpSelection = false; // Whether the player is in jump selection state
    private int currentLane = 0;
    private int currentPosition = 0;
    private float selectionCooldown = 0.2f; // Cooldown time between selection changes
    private float selectionTimer = 0f;

    public override void OnNetworkSpawn()
    {
        isActive.OnValueChanged += SetJumpPadActive;

        ExitJumpSelection();

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
        jumpPadAnimator.gameObject.SetActive(active);
    }

    private void Update()
    {
        if (currentJumper != null && currentJumper.IsOwner)
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
        if (!inJumpSelection)
        {
            if (playerControls.Movement.Recall.WasPressedThisFrame())
            {
                EnterJumpSelection();
            }
        }
        else
        {
            HandleIndicatorSelection();

            if (playerControls.Movement.MoveB.WasPressedThisFrame())
            {
                PerformJumpServerRPC(currentJumper.NetworkObjectId, currentLane, currentPosition);
            }
        }
    }

    private void EnterJumpSelection()
    {
        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_InGame_BigJump_Ready);

        inJumpSelection = true;
        landingIndicator.SetActive(true);
        lineObject.gameObject.SetActive(true);
        UpdateIndicatorPosition();

        CameraController.Instance.SetSuperJumpCamera(true);
    }

    private void ExitJumpSelection()
    {
        inJumpSelection = false;
        landingIndicator.SetActive(false);
        lineObject.gameObject.SetActive(false);
        currentLane = 0;
        currentPosition = 0;

        CameraController.Instance.SetSuperJumpCamera(false);
    }

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
    private void PerformJumpServerRPC(ulong playerID, int lane, int position)
    {
        PlayerManager player = NetworkManager.SpawnManager.SpawnedObjects[playerID].GetComponent<PlayerManager>();
        StartCoroutine(PerformJumpServer(player, lane, position));
    }

    private IEnumerator PerformJumpServer(PlayerManager player, int lane = 0, int position = 0)
    {
        Transform targetPosition = lanes[lane].GetChild(position);

        jumpPadAnimator.SetTrigger(GetJumpAnimation(lane));
        player.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, jumpTime, true, 0));

        StartJumpForPlayerRPC(player.NetworkObjectId, targetPosition.position, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));

        yield return new WaitForSeconds(jumpTime);

        UnlockPlayerMovementRPC(player.NetworkObjectId, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));

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
        PlayerManager jumper = NetworkManager.SpawnManager.SpawnedObjects[playerID].GetComponent<PlayerManager>();

        if (jumper != null)
        {
            StartCoroutine(PerformJump(jumper, targetPosition));
        }
    }

    private IEnumerator PerformJump(PlayerManager player, Vector3 targetPosition)
    {
        isJumping = true;
        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_InGame_BigJump_Fly);
        player.PlayerMovement.CanMove = false;
        player.transform.DOJump(targetPosition, jumpHeight, 1, jumpTime);

        yield return new WaitForSeconds(jumpTime);

        player.PlayerMovement.CanMove = true;

        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_InGame_BigJump_Done);

        ExitJumpSelection();

        isJumping = false;

        landingIndicator.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive.Value || !other.CompareTag("Player"))
        {
            return;
        }

        if (!other.TryGetComponent(out PlayerManager playerManager))
        {
            return;
        }

        if (playerManager.IsOwner)
        {
            currentJumper = playerManager;
            LockPlayerMovementRPC(playerManager.NetworkObjectId, RpcTarget.Single(playerManager.OwnerClientId, RpcTargetUse.Temp));
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

        if (currentJumper = playerManager)
        {
            currentJumper = null;

            if (!isJumping)
            {
                UnlockPlayerMovementRPC(playerManager.NetworkObjectId, RpcTarget.Single(playerManager.OwnerClientId, RpcTargetUse.Temp));
            }
            currentJumper = null;
            ExitJumpSelection();
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void LockPlayerMovementRPC(ulong playerID, RpcParams rpcParams = default)
    {
        PlayerManager player = NetworkManager.SpawnManager.SpawnedObjects[playerID].GetComponent<PlayerManager>();

        player.MovesController.AddMoveStatus(0, ActionStatusType.Busy);
        player.MovesController.AddMoveStatus(1, ActionStatusType.Busy);
        player.MovesController.AddMoveStatus(2, ActionStatusType.Busy);

        player.MovesController.BattleItemStatus.AddStatus(ActionStatusType.Busy);

        player.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Busy);

        player.RecallStatus.AddStatus(ActionStatusType.Busy);

        player.ScoreStatus.AddStatus(ActionStatusType.Busy);
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void UnlockPlayerMovementRPC(ulong playerID, RpcParams rpcParams = default)
    {
        PlayerManager player = NetworkManager.SpawnManager.SpawnedObjects[playerID].GetComponent<PlayerManager>();

        player.MovesController.RemoveMoveStatus(0, ActionStatusType.Busy);
        player.MovesController.RemoveMoveStatus(1, ActionStatusType.Busy);
        player.MovesController.RemoveMoveStatus(2, ActionStatusType.Busy);

        player.MovesController.BattleItemStatus.RemoveStatus(ActionStatusType.Busy);

        player.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Busy);

        player.RecallStatus.RemoveStatus(ActionStatusType.Busy);

        player.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }
}
