using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public enum CurrentDraftPhase : byte { Banning, Picking, Preparation }

public class DraftSelectController : NetworkBehaviour
{
    [SerializeField] private int BansPerTeam = 2;

    [SerializeField] private DraftPlayerHolder draftPlayerHolderBlue;
    [SerializeField] private DraftPlayerHolder draftPlayerHolderOrange;

    [SerializeField] private DraftTimerUI draftTimerUI;
    [SerializeField] private DraftCharacterSelector draftCharacterSelector;

    [SerializeField] private Button confirmButton, switchButton;

    List<Player> blueTeamPlayers;
    List<Player> orangeTeamPlayers;

    private NetworkList<byte> blueTeamPlayerStates;
    private NetworkList<byte> orangeTeamPlayerStates;

    private NetworkVariable<CurrentDraftPhase> currentDraftPhase = new NetworkVariable<CurrentDraftPhase>(CurrentDraftPhase.Banning);
    private NetworkVariable<DraftTurnData> currentDraftTurnData = new NetworkVariable<DraftTurnData>(new DraftTurnData());
    private NetworkVariable<float> draftTimer = new NetworkVariable<float>(0f);

    private int currentBanIndex = 0;
    private int currentPickIndex = 0;

    private void Awake()
    {
        blueTeamPlayerStates = new NetworkList<byte>();
        orangeTeamPlayerStates = new NetworkList<byte>();
    }

    public override void OnNetworkSpawn()
    {
        blueTeamPlayers = LobbyController.Instance.GetTeamPlayers(false).ToList();
        orangeTeamPlayers = LobbyController.Instance.GetTeamPlayers(true).ToList();

        draftCharacterSelector.InitializeUI();
        draftCharacterSelector.OnCharacterSelected += OnCharacterSelected;

        foreach (Player player in blueTeamPlayers)
        {
            blueTeamPlayerStates.Add((byte)DraftPlayerState.Idle);
        }

        foreach (Player player in orangeTeamPlayers)
        {
            orangeTeamPlayerStates.Add((byte)DraftPlayerState.Idle);
        }

        draftPlayerHolderBlue.Initialize(blueTeamPlayers);
        draftPlayerHolderOrange.Initialize(orangeTeamPlayers);

        if (IsServer)
        {
            StartBanningPhase();
        }
    }

    private void StartBanningPhase()
    {
        currentDraftPhase.Value = CurrentDraftPhase.Banning;
        currentBanIndex = 0;

        UpdateBanTurn();
    }

    private void UpdateBanTurn()
    {
        if (currentBanIndex >= BansPerTeam * 2)
        {
            StartPickingPhase();
            return;
        }

        DraftTurnData draftTurnData = new DraftTurnData();
        draftTurnData.IsOrangeTeamTurn = currentBanIndex % 2 != 0;

        int playerIndex = currentBanIndex / 2;

        draftTurnData.SelectedPlayerIDs = draftTurnData.IsOrangeTeamTurn
            ? new ushort[] { (ushort)(orangeTeamPlayers.Count - 1 - playerIndex) }
            : new ushort[] { (ushort)(blueTeamPlayers.Count - 1 - playerIndex) };

        currentDraftTurnData.Value = draftTurnData;
        currentBanIndex++;
        draftTimer.Value = 25f;

        UpdateSelectedPlayers();
    }

    private void StartPickingPhase()
    {
        currentDraftPhase.Value = CurrentDraftPhase.Picking;
        currentPickIndex = 0;

        UpdatePickTurn();
    }

    private void UpdatePickTurn()
    {
        if (currentPickIndex >= blueTeamPlayers.Count + orangeTeamPlayers.Count)
        {
            StartPreparationPhase();
            return;
        }

        DraftTurnData draftTurnData = new DraftTurnData();

        // Follow the specific pick order
        if (currentPickIndex == 0 || currentPickIndex % 2 == 0)
        {
            draftTurnData.IsOrangeTeamTurn = (currentPickIndex % 4 != 0);
        }
        else
        {
            draftTurnData.IsOrangeTeamTurn = (currentPickIndex % 4 == 1);
        }

        int playerIndex = currentPickIndex / 2;

        draftTurnData.SelectedPlayerIDs = draftTurnData.IsOrangeTeamTurn
            ? new ushort[] { (ushort)playerIndex }
            : new ushort[] { (ushort)playerIndex };

        currentDraftTurnData.Value = draftTurnData;
        currentPickIndex++;
        draftTimer.Value = 25f;

        UpdateSelectedPlayers();
    }

    private void StartPreparationPhase()
    {
        currentDraftPhase.Value = CurrentDraftPhase.Preparation;
        draftTimer.Value = 60f; // Example time for preparation

        // Notify players they can switch heroes or change equipment
        UpdateSelectedPlayers();
    }

    private void UpdateSelectedPlayers()
    {
        if (!IsServer)
        {
            return;
        }

        for (int i = 0; i < blueTeamPlayerStates.Count; i++)
        {
            if (blueTeamPlayerStates[i] == (byte)DraftPlayerState.Confirmed)
            {
                continue;
            }
            blueTeamPlayerStates[i] = (byte)DraftPlayerState.Idle;
        }

        for (int i = 0; i < orangeTeamPlayerStates.Count; i++)
        {
            if (orangeTeamPlayerStates[i] == (byte)DraftPlayerState.Confirmed)
            {
                continue;
            }
            orangeTeamPlayerStates[i] = (byte)DraftPlayerState.Idle;
        }

        DraftTurnData draftTurnData = currentDraftTurnData.Value;
        bool isBanningPhase = currentDraftPhase.Value == CurrentDraftPhase.Banning;
        bool isPickingPhase = currentDraftPhase.Value == CurrentDraftPhase.Picking;

        // Determine if it's the current player's turn
        bool isPlayerTurn = draftTurnData.IsOrangeTeamTurn ?
            orangeTeamPlayerStates[draftTurnData.SelectedPlayerIDs[0]] == (byte)DraftPlayerState.Idle :
            blueTeamPlayerStates[draftTurnData.SelectedPlayerIDs[0]] == (byte)DraftPlayerState.Idle;

        // Toggle the confirm button during banning or picking if it's the player's turn
        confirmButton.gameObject.SetActive((isBanningPhase || isPickingPhase) && isPlayerTurn);

        // Toggle the switch button only during the Preparation phase
        switchButton.gameObject.SetActive(currentDraftPhase.Value == CurrentDraftPhase.Preparation);

        if (draftTurnData.IsOrangeTeamTurn)
        {
            foreach (ushort playerID in draftTurnData.SelectedPlayerIDs)
            {
                orangeTeamPlayerStates[playerID] = currentDraftPhase.Value == CurrentDraftPhase.Banning ? (byte)DraftPlayerState.Banning : (byte)DraftPlayerState.Picking;
            }
        }
        else
        {
            foreach (ushort playerID in draftTurnData.SelectedPlayerIDs)
            {
                blueTeamPlayerStates[playerID] = currentDraftPhase.Value == CurrentDraftPhase.Banning ? (byte)DraftPlayerState.Banning : (byte)DraftPlayerState.Picking;
            }
        }

        UpdatePlayerIconsRPC();
    }

    [Rpc(SendTo.Server)]
    private void UpdatePlayerStateRPC(ushort playerID, bool orangeSide, DraftPlayerState state)
    {
        if (currentDraftPhase.Value == CurrentDraftPhase.Preparation)
        {
            return;
        }

        if (orangeSide)
        {
            orangeTeamPlayerStates[playerID] = (byte)state;
        }
        else
        {
            blueTeamPlayerStates[playerID] = (byte)state;
        }

        UpdatePlayerIconsRPC();

        if (state == DraftPlayerState.Confirmed)
        {
            if (currentDraftPhase.Value == CurrentDraftPhase.Banning)
            {
                UpdateBanTurn();
            }
            else if (currentDraftPhase.Value == CurrentDraftPhase.Picking)
            {
                UpdatePickTurn();
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdatePlayerIconsRPC()
    {
        foreach (DraftPlayerIcon playerIcon in draftPlayerHolderBlue.PlayerIcons)
        {
            playerIcon.UpdateIconState((DraftPlayerState)blueTeamPlayerStates[blueTeamPlayers.IndexOf(playerIcon.AssignedPlayer)]);
        }

        foreach (DraftPlayerIcon playerIcon in draftPlayerHolderOrange.PlayerIcons)
        {
            playerIcon.UpdateIconState((DraftPlayerState)orangeTeamPlayerStates[orangeTeamPlayers.IndexOf(playerIcon.AssignedPlayer)]);
        }
    }

    [Rpc(SendTo.Server)]
    private void OnPlayerConfirmedCharacter(ushort playerID, int selectedCharacter)
    {
        DraftTurnData draftTurnData = currentDraftTurnData.Value;

        // Verify that the player is allowed to confirm
        bool isOrangeTeamTurn = draftTurnData.IsOrangeTeamTurn;
        bool isPlayerTurn = true; // Implement this

        if (!isPlayerTurn)
        {
            Debug.LogWarning("Player tried to confirm out of turn!");
            return;
        }

        // Apply the confirmed character
        if (currentDraftPhase.Value == CurrentDraftPhase.Banning)
        {
            // Add selectedCharacter to the banned characters list (implement this)
        }
        else if (currentDraftPhase.Value == CurrentDraftPhase.Picking)
        {
            // Assign selectedCharacter to the player's selected character (implement this)
        }

        // Update the player's state to confirmed
        if (isOrangeTeamTurn)
        {
            orangeTeamPlayerStates[playerID] = (byte)DraftPlayerState.Confirmed;
        }
        else
        {
            blueTeamPlayerStates[playerID] = (byte)DraftPlayerState.Confirmed;
        }

        UpdatePlayerIconsRPC(); // Update the UI to reflect the new state

        // Progress to the next turn
        if (currentDraftPhase.Value == CurrentDraftPhase.Banning)
        {
            UpdateBanTurn();
        }
        else if (currentDraftPhase.Value == CurrentDraftPhase.Picking)
        {
            UpdatePickTurn();
        }
    }

    private void OnCharacterSelected(CharacterInfo characterInfo)
    {
        ushort playerID = (ushort)GetCurrentPlayerIndex();
        CharactersList.Instance.Characters.ToList().IndexOf(characterInfo);
    }

    public int GetCurrentPlayerIndex()
    {
        bool localPlayerTeam = LobbyController.Instance.GetLocalPlayerTeam();

        if (localPlayerTeam)
        {
            return orangeTeamPlayers.FindIndex(player => player == LobbyController.Instance.Player);
        }
        else
        {
            return blueTeamPlayers.FindIndex(player => player == LobbyController.Instance.Player);
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
            // Handle timeouts, maybe auto-confirm the selection
            // or progress to the next phase automatically.
        }
    }
}

public struct DraftTurnData : INetworkSerializable
{
    public bool IsOrangeTeamTurn;
    public ushort[] SelectedPlayerIDs;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref IsOrangeTeamTurn);
        serializer.SerializeValue(ref SelectedPlayerIDs);
    }
}
