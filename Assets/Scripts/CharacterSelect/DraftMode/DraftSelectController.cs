using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public enum DraftPhase : byte { Banning, Picking, Preparation }

public class DraftSelectController : NetworkBehaviour
{
    [SerializeField] private int maxBansPerTeam = 2;
    [SerializeField] private DraftPlayerHolder draftPlayerHolderBlue;
    [SerializeField] private DraftPlayerHolder draftPlayerHolderOrange;
    [SerializeField] private DraftTimerUI draftTimerUI;
    [SerializeField] private DraftCharacterSelector draftCharacterSelector;
    [SerializeField] private Button confirmButton, switchButton;

    private List<Player> blueTeamPlayers;
    private List<Player> orangeTeamPlayers;

    private NetworkVariable<DraftPhase> currentDraftPhase = new NetworkVariable<DraftPhase>(DraftPhase.Banning);
    private NetworkVariable<int> currentTurnIndex = new NetworkVariable<int>(0);
    private NetworkVariable<float> draftTimer = new NetworkVariable<float>(0f);

    private NetworkList<FixedString32Bytes> playerIDs;
    private NetworkList<byte> playerStates;

    public event Action<DraftPhase> OnPhaseChanged;
    public event Action<string, int> OnPlayerConfirmed;

    private void Awake()
    {
        playerIDs = new NetworkList<FixedString32Bytes>();
        playerStates = new NetworkList<byte>();
    }

    public override void OnNetworkSpawn()
    {
        blueTeamPlayers = LobbyController.Instance.GetTeamPlayers(false).ToList();
        orangeTeamPlayers = LobbyController.Instance.GetTeamPlayers(true).ToList();

        currentDraftPhase.OnValueChanged += (_, phase) => OnPhaseChanged?.Invoke(phase);

        InitializeUI();

        if (IsServer)
        {
            InitializePlayerStates();

            StartPhase(DraftPhase.Banning);
        }
    }

    private void InitializePlayerStates()
    {
        foreach (Player player in blueTeamPlayers)
        {
            playerIDs.Add(player.Id);
            playerStates.Add((byte)DraftPlayerState.Idle);
        }
        foreach (Player player in orangeTeamPlayers)
        {
            playerIDs.Add(player.Id);
            playerStates.Add((byte)DraftPlayerState.Idle);
        }
    }

    private void InitializeUI()
    {
        draftCharacterSelector.InitializeUI();
        draftCharacterSelector.OnCharacterSelected += OnCharacterSelected;

        draftPlayerHolderBlue.Initialize(blueTeamPlayers);
        draftPlayerHolderOrange.Initialize(orangeTeamPlayers);

        confirmButton.onClick.AddListener(OnConfirmButtonClicked);
        switchButton.onClick.AddListener(OnSwitchButtonClicked);
    }

    private void StartPhase(DraftPhase phase)
    {
        if (!IsServer)
        {
            return;
        }

        currentDraftPhase.Value = phase;
        currentTurnIndex.Value = 0;
        draftTimer.Value = GetPhaseTimeLimit(phase);

        UpdateUIForCurrentTurnRPC();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateUIForCurrentTurnRPC()
    {
        if (IsCurrentPlayerTurn(LobbyController.Instance.Player.Id))
        {
            confirmButton.gameObject.SetActive(currentDraftPhase.Value != DraftPhase.Preparation);
            switchButton.gameObject.SetActive(currentDraftPhase.Value == DraftPhase.Preparation);
        }
        else
        {
            confirmButton.gameObject.SetActive(false);
            switchButton.gameObject.SetActive(false);
        }

        UpdatePlayerIcons();
    }

    private void OnConfirmButtonClicked()
    {
        string playerID = LobbyController.Instance.Player.Id;
        int selectedCharacter = draftCharacterSelector.GetSelectedCharacterID();

        if (IsCurrentPlayerTurn(playerID) && IsServer)
        {
            OnPlayerConfirmed?.Invoke(playerID, selectedCharacter);
            HandlePlayerConfirmationRPC(playerID, selectedCharacter);
        }
    }

    private void OnSwitchButtonClicked()
    {
        // Handle switch logic (e.g., switching selected character during preparation)
    }

    [Rpc(SendTo.Server)]
    private void HandlePlayerConfirmationRPC(string playerID, int selectedCharacter)
    {
        int index = GetPlayerIndex(playerID);
        if (index == -1)
        {
            Debug.LogWarning($"PlayerID {playerID} not found in player list.");
            return;
        }

        if (currentDraftPhase.Value == DraftPhase.Banning)
        {
            // Handle ban logic
            playerStates[index] = (byte)DraftPlayerState.Idle;
        }
        else if (currentDraftPhase.Value == DraftPhase.Picking)
        {
            // Handle pick logic
            playerStates[index] = (byte)DraftPlayerState.Confirmed;
        }

        ProgressTurn();
    }

    private void ProgressTurn()
    {
        if (!IsServer)
        {
            return;
        }

        currentTurnIndex.Value++;

        if (IsPhaseComplete())
        {
            if (currentDraftPhase.Value == DraftPhase.Banning)
            {
                StartPhase(DraftPhase.Picking);
            }
            else if (currentDraftPhase.Value == DraftPhase.Picking)
            {
                StartPhase(DraftPhase.Preparation);
            }
            else
            {
                // Start the match or end the draft
                StartMatch();
            }
        }
        else
        {
            UpdateUIForCurrentTurnRPC();
        }
    }

    private bool IsPhaseComplete()
    {
        int totalTurns = currentDraftPhase.Value == DraftPhase.Banning
            ? maxBansPerTeam * 2
            : (blueTeamPlayers.Count + orangeTeamPlayers.Count) * 2;
        return currentTurnIndex.Value >= totalTurns;
    }

    private bool IsCurrentPlayerTurn(string playerID)
    {
        return GetCurrentPlayerID() == playerID;
    }

    private int GetPlayerIndex(string playerID)
    {
        return playerIDs.IndexOf(playerID);
    }

    private string GetCurrentPlayerID()
    {
        if (currentDraftPhase.Value == DraftPhase.Banning)
        {
            return GetBanTurnPlayer();
        }
        else if (currentDraftPhase.Value == DraftPhase.Picking)
        {
            return GetPickTurnPlayer();
        }

        return string.Empty;
    }

    private string GetBanTurnPlayer()
    {
        int playerIndex = (maxBansPerTeam * 2) - 1 - currentTurnIndex.Value;
        if (playerIndex % 2 == 0)
        {
            return orangeTeamPlayers[playerIndex / 2].Id;
        }
        else
        {
            return blueTeamPlayers[playerIndex / 2].Id;
        }
    }

    private string GetPickTurnPlayer()
    {
        if (currentTurnIndex.Value == 0)
        {
            return blueTeamPlayers[0].Id;
        }

        int adjustedIndex = (currentTurnIndex.Value - 1) / 2;
        if (currentTurnIndex.Value % 2 == 0)
        {
            return orangeTeamPlayers[adjustedIndex % orangeTeamPlayers.Count].Id;
        }
        else
        {
            return blueTeamPlayers[(adjustedIndex + 1) % blueTeamPlayers.Count].Id;
        }
    }

    private float GetPhaseTimeLimit(DraftPhase phase)
    {
        return phase switch
        {
            DraftPhase.Banning => 20f,
            DraftPhase.Picking => 25f,
            DraftPhase.Preparation => 30f,
            _ => 0f,
        };
    }

    private void OnCharacterSelected(CharacterInfo characterInfo)
    {
        // Handle character selection logic here
    }

    private void UpdatePlayerIcons()
    {
        for (int i = 0; i < playerIDs.Count; i++)
        {
            string playerID = playerIDs[i].ToString();
            DraftPlayerState state = (DraftPlayerState)playerStates[i];

            // Update the blue team icons
            foreach (var playerIcon in draftPlayerHolderBlue.PlayerIcons)
            {
                if (playerIcon.AssignedPlayer.Id == playerID)
                {
                    playerIcon.UpdateIconState(state);
                }
            }

            // Update the orange team icons
            foreach (var playerIcon in draftPlayerHolderOrange.PlayerIcons)
            {
                if (playerIcon.AssignedPlayer.Id == playerID)
                {
                    playerIcon.UpdateIconState(state);
                }
            }
        }
    }

    private void Update()
    {
        draftTimerUI.UpdateTimer(draftTimer.Value);

        if (!IsServer)
        {
            return;
        }

        if (draftTimer.Value > 0)
        {
            draftTimer.Value -= Time.deltaTime;
        }
        else
        {
            HandleTimeout();
        }
    }

    private void HandleTimeout()
    {
        string playerID = GetCurrentPlayerID(); // Get the current player's ID
        int playerIndex = GetPlayerIndex(playerID); // Get the index of the player

        if (playerIndex != -1 && playerStates[playerIndex] != (byte)DraftPlayerState.Confirmed)
        {
            // Auto-confirm the current selection or perform other timeout actions
            int selectedCharacterID = draftCharacterSelector.GetSelectedCharacterID();
            OnPlayerConfirmed?.Invoke(playerID, selectedCharacterID);
            HandlePlayerConfirmationRPC(playerID, selectedCharacterID);
        }
    }

    private void StartMatch()
    {
        // Add logic to start the match here

        Debug.Log("Draft phase complete! Starting match...");
    }
}
