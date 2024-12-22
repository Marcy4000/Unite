using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening;
using JSAM;

public class SmallJumpPad : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Animator jumpPadAnimator;
    [SerializeField] private Transform landingPosition;
    [SerializeField] private GameObject gaugeHolder;
    [SerializeField] private Image gaugeFill;

    [Header("Settings")]
    [SerializeField] private float jumpTime = 1f;
    [SerializeField] private float jumpHeight = 5f;
    [SerializeField] private float jumpChargeTime = 2.5f; // Time to fully charge the jump
    [SerializeField] private float spawnTime;
    [SerializeField] private float despawnTime;

    private NetworkVariable<bool> isActive = new NetworkVariable<bool>(true);

    private List<PlayerManager> playersInPad = new List<PlayerManager>();
    private PlayerManager currentJumper;
    private NetworkVariable<float> chargeTimer = new NetworkVariable<float>();
    private float cooldownTimer = 0f; // Delay between jumps
    private bool isJumping = false;

    public override void OnNetworkSpawn()
    {
        isActive.OnValueChanged += SetJumpPadActive;
        chargeTimer.OnValueChanged += UpdateGauge;

        if (!IsServer)
        {
            return;
        }

        ShowGaugeToPlayerRPC(NetworkManager.LocalClientId, false);

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

        if (!isActive.Value)
        {
            return;
        }

        HandleJumpLogic();
    }

    private void HandleJumpLogic()
    {
        if (isJumping || cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            return;
        }

        if (playersInPad.Count > 0 && currentJumper == null)
        {
            currentJumper = playersInPad[0];
            chargeTimer.Value = 0f;
            LockPlayerMovementRPC(currentJumper.NetworkObjectId, RpcTarget.Single(currentJumper.OwnerClientId, RpcTargetUse.Temp)); // Lock movement of the player
            ShowGaugeToPlayerRPC(currentJumper.OwnerClientId, true);
        }

        if (currentJumper != null)
        {
            chargeTimer.Value += Time.deltaTime;

            if (chargeTimer.Value >= jumpChargeTime)
            {
                StartCoroutine(PerformJumpServer(currentJumper));
                StartJumpForPlayerRPC(currentJumper.NetworkObjectId, RpcTarget.Single(currentJumper.OwnerClientId, RpcTargetUse.Temp));
            }
        }
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void StartJumpForPlayerRPC(ulong playerID, RpcParams rpcParams = default)
    {
        PlayerManager jumper = NetworkManager.SpawnManager.SpawnedObjects[playerID].GetComponent<PlayerManager>();

        if (jumper != null)
        {
            StartCoroutine(PerformJump(jumper));
        }
    }

    private IEnumerator PerformJump(PlayerManager player)
    {
        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_InGame_SmallJump);
        player.PlayerMovement.CanMove = false; // Lock movement before jump starts
        player.transform.DOJump(landingPosition.position, jumpHeight, 1, jumpTime);

        yield return new WaitForSeconds(jumpTime); // Simulate jump duration

        player.PlayerMovement.CanMove = true;
    }

    private IEnumerator PerformJumpServer(PlayerManager player)
    {
        isJumping = true;
        ShowGaugeToPlayerRPC(player.OwnerClientId, false);
        jumpPadAnimator.Play("Jump");
        player.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, jumpTime, true, 0));

        yield return new WaitForSeconds(jumpTime); // Simulate jump duration

        UnlockPlayerMovementRPC(player.NetworkObjectId, RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp)); // Unlock movement after jump starts
        currentJumper = null;
        playersInPad.Remove(player);
        isJumping = false;
        cooldownTimer = 0.6f; // Add delay between jumps
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !isActive.Value || !other.CompareTag("Player"))
        {
            return;
        }

        if (!other.TryGetComponent(out PlayerManager playerManager))
        {
            return;
        }

        if (!playersInPad.Contains(playerManager))
        {
            playersInPad.Add(playerManager);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer || !other.CompareTag("Player"))
        {
            return;
        }

        if (!other.TryGetComponent(out PlayerManager playerManager))
        {
            return;
        }

        if (playersInPad.Contains(playerManager))
        {
            playersInPad.Remove(playerManager);

            if (playerManager == currentJumper)
            {
                ShowGaugeToPlayerRPC(playerManager.OwnerClientId, false);
                UnlockPlayerMovementRPC(playerManager.NetworkObjectId, RpcTarget.Single(playerManager.OwnerClientId, RpcTargetUse.Temp)); // Unlock movement after jump starts
                currentJumper = null;
                chargeTimer.Value = 0f; // Reset charge time
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowGaugeToPlayerRPC(ulong clientToShow, bool show)
    {
        gaugeHolder.SetActive(clientToShow == NetworkManager.LocalClientId ? show : false);
    }

    private void UpdateGauge(float prevValue, float fillAmount)
    {
        gaugeFill.fillAmount = fillAmount / jumpChargeTime;
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

        player.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
    }
}
