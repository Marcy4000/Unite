using JSAM;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum DraftPhase : byte { Banning, Picking, Preparation, Starting, Waiting }

public class DraftSelectController : NetworkBehaviour
{
    public static DraftSelectController Instance { get; private set; }

    [SerializeField] private int maxBansPerTeam = 2;
    [SerializeField] private DraftPlayerHolder draftPlayerHolderBlue;
    [SerializeField] private DraftPlayerHolder draftPlayerHolderOrange;
    [SerializeField] private DraftTimerUI draftTimerUI;
    [SerializeField] private DraftCharacterSelector draftCharacterSelector;
    [SerializeField] private DraftBansShowcase draftBansShowcase;
    [SerializeField] private Button confirmButton, switchButton, battlePrepButton;
    [SerializeField] private GameObject background;
    [SerializeField] private Transform pokemonSpawnPoint;
    [SerializeField] private TrainerModel trainerModel;

    [Space]
    [SerializeField] private Transform allyBanHolder, enemyBanHolder;
    [SerializeField] private GameObject banIconPrefab;

    private GameObject currentPokemon;
    private AsyncOperationHandle<GameObject> characterSelectModelHandle;

    private int currentAllyBanIndex = 0;
    private int currentEnemyBanIndex = 0;

    private List<Player> blueTeamPlayers;
    private List<Player> orangeTeamPlayers;

    private List<Player> AllyTeamPlayers => LobbyController.Instance.GetLocalPlayerTeam() ? orangeTeamPlayers : blueTeamPlayers;
    private List<Player> EnemyTeamPlayers => LobbyController.Instance.GetLocalPlayerTeam() ? blueTeamPlayers : orangeTeamPlayers;

    // Network-synced variables
    private NetworkVariable<DraftPhase> currentDraftPhase = new NetworkVariable<DraftPhase>(DraftPhase.Waiting);
    private NetworkVariable<int> currentTurnIndex = new NetworkVariable<int>(0);
    private NetworkVariable<float> draftTimer = new NetworkVariable<float>(0f);

    private NetworkList<FixedString32Bytes> playerIDs;
    private NetworkList<byte> playerStates;
    private NetworkList<short> bannedCharacters;


    public NetworkList<short> BannedCharacters => bannedCharacters;

    public event Action<DraftPhase> OnPhaseChanged;
    public event Action<string, int> OnPlayerConfirmed;

    private void Awake()
    {
        playerIDs = new NetworkList<FixedString32Bytes>();
        playerStates = new NetworkList<byte>();
        bannedCharacters = new NetworkList<short>();

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        blueTeamPlayers = LobbyController.Instance.GetTeamPlayers(false).ToList();
        orangeTeamPlayers = LobbyController.Instance.GetTeamPlayers(true).ToList();

        InitializeUI();

        // Listen for network value changes
        currentDraftPhase.OnValueChanged += OnDraftPhaseChanged;
        currentTurnIndex.OnValueChanged += OnTurnIndexChanged;
        draftTimer.OnValueChanged += UpdateTimerUI;
        playerStates.OnListChanged += OnPlayerStatesChanged;
        bannedCharacters.OnListChanged += (listEvent) => { draftCharacterSelector.UpdateUnavailablePokemons(); };
        LobbyController.Instance.LobbyEvents.Callbacks.PlayerDataChanged += (change) =>
        {
            foreach (var player in change)
            {
                if (player.Value.ContainsKey("SelectedCharacter"))
                {
                    draftCharacterSelector.UpdateUnavailablePokemons();
                    UpdatePlayerSelectedCharacter(LobbyController.Instance.Lobby.Players[player.Key].Id, NumberEncoder.FromBase64<short>(player.Value["SelectedCharacter"].Value.Value));
                }
            }
        };

        if (IsServer)
        {
            InitializePlayerStates();
        }

        if (IsClient)
        {
            LobbyController.Instance.ChangePlayerCharacter(-1);
            trainerModel.InitializeClothes(PlayerClothesInfo.Deserialize(LobbyController.Instance.Player.Data["ClothingInfo"].Value));
        }

        AudioManager.StopAllMusic();
        AudioManager.PlayMusic(DefaultAudioMusic.ChoosePokemon);
    }

    private void OnEnable()
    {
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName == "DraftSelect")
        {
            if (IsServer)
            {
                StartPhase(DraftPhase.Banning);
            }
            OnTurnIndexChanged(0, 0);
            LoadingScreen.Instance.HideGameBeginScreen();
        }
    }

    private void OnPlayerStatesChanged(NetworkListEvent<byte> listEvent)
    {
        if (listEvent.Type == NetworkListEvent<byte>.EventType.Value)
        {
            UpdateUIForCurrentTurn();
        }
    }

    private void InitializePlayerStates()
    {
        foreach (Player player in AllyTeamPlayers)
        {
            playerIDs.Add(player.Id);
            playerStates.Add((byte)DraftPlayerState.Idle);
        }
        foreach (Player player in EnemyTeamPlayers)
        {
            playerIDs.Add(player.Id);
            playerStates.Add((byte)DraftPlayerState.Idle);
        }
    }

    private void InitializeUI()
    {
        draftCharacterSelector.InitializeUI();
        draftCharacterSelector.OnCharacterSelected += OnCharacterSelected;
        draftCharacterSelector.OnSelectedToggleChanged += UpdateHoveredCharacter;

        draftPlayerHolderBlue.Initialize(AllyTeamPlayers);
        draftPlayerHolderOrange.Initialize(EnemyTeamPlayers);

        switchButton.onClick.AddListener(OnSwitchButtonClicked);
        battlePrepButton.gameObject.SetActive(false);

        for (int i = 0; i < maxBansPerTeam; i++)
        {
            GameObject banIconBlue = Instantiate(banIconPrefab, allyBanHolder);
            GameObject banIconOrange = Instantiate(banIconPrefab, enemyBanHolder);
        }
    }

    private void StartPhase(DraftPhase phase)
    {
        if (!IsServer)
        {
            return;
        }

        // Set the current draft phase
        currentDraftPhase.Value = phase;

        // Reset the turn index to 0 (start with the first player)
        currentTurnIndex.Value = 0;

        // Set the draft timer based on the phase
        draftTimer.Value = GetPhaseTimeLimit(phase);

        // Reset player states based on the phase
        for (int i = 0; i < playerStates.Count; i++)
        {
            playerStates[i] = (byte)DraftPlayerState.Idle;
        }

        string[] selectedPlayers = GetCurrentPlayerIDs().ToArray();

        switch (currentDraftPhase.Value)
        {
            case DraftPhase.Banning:
                for (int i = playerStates.Count - 1; i >= playerStates.Count - maxBansPerTeam; i--)
                {
                    playerStates[i] = (byte)DraftPlayerState.BanningIdle;
                }

                foreach (string playerID in selectedPlayers)
                {
                    int index = GetPlayerIndex(playerID);
                    if (index != -1)
                    {
                        playerStates[index] = (byte)DraftPlayerState.Banning;
                    }
                }
                break;
            case DraftPhase.Picking:
                foreach (string playerID in selectedPlayers)
                {
                    int index = GetPlayerIndex(playerID);
                    if (index != -1)
                    {
                        playerStates[index] = (byte)DraftPlayerState.Picking;
                    }
                }
                break;
            case DraftPhase.Preparation:
                for (int i = 0; i < playerStates.Count; i++)
                {
                    playerStates[i] = (byte)DraftPlayerState.Confirmed;
                }
                break;
        }
    }

    // Handle phase changes
    private void OnDraftPhaseChanged(DraftPhase previous, DraftPhase current)
    {
        Debug.Log($"Draft phase changed to {current}");

        if (current == DraftPhase.Picking)
        {
            StartCoroutine(ShowBansWhenUpdated());
        }

        OnPhaseChanged?.Invoke(current);
    }

    private IEnumerator ShowBansWhenUpdated()
    {
        int totalBansRequired = Mathf.Min(blueTeamPlayers.Count, maxBansPerTeam) +
                                Mathf.Min(orangeTeamPlayers.Count, maxBansPerTeam);

        while (bannedCharacters.Count < totalBansRequired)
        {
            yield return null;
        }

        List<CharacterInfo> bannedCharactersInfo = new List<CharacterInfo>();
        foreach (short bannedCharacter in bannedCharacters)
        {
            bannedCharactersInfo.Add(CharactersList.Instance.GetCharacterFromID(bannedCharacter));
        }

        draftBansShowcase.ShowBans(bannedCharactersInfo);
    }


    private void OnTurnIndexChanged(int previous, int current)
    {
        UpdatePlayerIcons();

        switch (currentDraftPhase.Value)
        {
            case DraftPhase.Banning:

                if ((current % 2 == 0) == !LobbyController.Instance.GetLocalPlayerTeam())
                {
                    draftTimerUI.DoFadeIn(0);
                    draftTimerUI.UpdateMessage("Your team is banning");
                }
                else
                {
                    draftTimerUI.DoFadeIn(1);
                    draftTimerUI.UpdateMessage("The opponent team is banning");
                }
                break;
            case DraftPhase.Picking:
                if ((current % 2 == 0) == !LobbyController.Instance.GetLocalPlayerTeam())
                {
                    draftTimerUI.DoFadeIn(0);
                    draftTimerUI.UpdateMessage("Your team is picking pokemon");
                }
                else
                {
                    draftTimerUI.DoFadeIn(1);
                    draftTimerUI.UpdateMessage("The opponent team is picking pokemon");
                }
                break;
            case DraftPhase.Preparation:
                draftTimerUI.DoFadeIn(2);
                draftTimerUI.UpdateMessage("Final Preparations");
                break;
        }
    }

    [Rpc(SendTo.Server)]
    private void ConfirmSelectionRPC(string playerID, int selectedCharacter)
    {
        int index = GetPlayerIndex(playerID);
        if (index == -1)
        {
            Debug.LogWarning($"PlayerID {playerID} not found in player list.");
            return;
        }

        string[] currentPlayers = GetCurrentPlayerIDs().ToArray();
        if (!currentPlayers.Contains(playerID))
        {
            Debug.LogWarning($"PlayerID {playerID} is not in the current player list.");
            return;
        }

        if (currentDraftPhase.Value == DraftPhase.Banning)
        {
            playerStates[index] = (byte)DraftPlayerState.BanningConfirmed; // Update state after banning
            bannedCharacters.Add((short)selectedCharacter);
        }
        else if (currentDraftPhase.Value == DraftPhase.Picking)
        {
            playerStates[index] = (byte)DraftPlayerState.Confirmed; // Update state after picking
        }

        HandlePlayerConfirmationRPC(playerID, selectedCharacter, currentDraftPhase.Value == DraftPhase.Banning);

        ProgressTurn();
    }

    [Rpc(SendTo.Everyone)]
    private void HandlePlayerConfirmationRPC(string playerID, int selectedCharacter, bool isBan)
    {
        // Notify clients and progress
        OnPlayerConfirmed?.Invoke(playerID, selectedCharacter);

        if (IsClient)
        {
            if (LobbyController.Instance.Player.Id == playerID && !isBan)
            {
                CharacterInfo selectedCharacterInfo = CharactersList.Instance.Characters[selectedCharacter];
                LobbyController.Instance.ChangePlayerCharacter((short)selectedCharacter);
                SpawnPokemon(selectedCharacterInfo);
                draftCharacterSelector.gameObject.SetActive(false);
                battlePrepButton.gameObject.SetActive(true);
            }
            else if (isBan)
            {
                bool isAllyBan = AllyTeamPlayers.Any(player => player.Id == playerID);

                if (isAllyBan)
                {
                    Transform banHolder = allyBanHolder.GetChild(currentAllyBanIndex);
                    banHolder.GetComponent<DraftBanHolder>().SetBanIcon(CharactersList.Instance.GetCharacterFromID((short)selectedCharacter));
                    currentAllyBanIndex++;
                }
                else
                {
                    Transform banHolder = enemyBanHolder.GetChild(currentEnemyBanIndex);
                    banHolder.GetComponent<DraftBanHolder>().SetBanIcon(CharactersList.Instance.GetCharacterFromID((short)selectedCharacter));
                    currentEnemyBanIndex++;
                }
            }
        }
    }

    private void ProgressTurn()
    {
        if (!IsServer)
        {
            return;
        }

        List<string> currentTurnPlayers = GetCurrentPlayerIDs();
        foreach (string playerID in currentTurnPlayers)
        {
            int index = GetPlayerIndex(playerID);
            if (index != -1 && playerStates[index] != (byte)DraftPlayerState.Confirmed && playerStates[index] != (byte)DraftPlayerState.BanningConfirmed)
            {
                return; // Wait for both players to confirm their picks
            }
        }

        // Progress the turn once both players have confirmed
        currentTurnIndex.Value += 1;

        draftTimer.Value = GetPhaseTimeLimit(currentDraftPhase.Value);

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
                StartMatch();
            }
        }
        else
        {
            UpdatePlayerStates();
        }
    }

    private void UpdatePlayerStates()
    {
        if (!IsServer)
        {
            return;
        }

        string[] selectedPlayers = GetCurrentPlayerIDs().ToArray();

        switch (currentDraftPhase.Value)
        {
            case DraftPhase.Banning:
                foreach (string playerID in selectedPlayers)
                {
                    int index = GetPlayerIndex(playerID);
                    if (index != -1)
                    {
                        playerStates[index] = (byte)DraftPlayerState.Banning;
                    }
                }
                break;
            case DraftPhase.Picking:
                foreach (string playerID in selectedPlayers)
                {
                    int index = GetPlayerIndex(playerID);
                    if (index != -1)
                    {
                        playerStates[index] = (byte)DraftPlayerState.Picking;
                    }
                }
                break;
        }
    }

    private bool IsPhaseComplete()
    {
        int totalTurns;

        if (currentDraftPhase.Value == DraftPhase.Banning)
        {
            // Calculate total bans based on actual team sizes
            int blueTeamBans = Mathf.Min(blueTeamPlayers.Count, maxBansPerTeam);
            int orangeTeamBans = Mathf.Min(orangeTeamPlayers.Count, maxBansPerTeam);

            // Total turns is the sum of bans from both teams
            totalTurns = blueTeamBans + orangeTeamBans;
        }
        else
        {
            // Picking phase logic: total turns is the sum of both team sizes
            totalTurns = blueTeamPlayers.Count + orangeTeamPlayers.Count;
        }

        // Check if the current turn index has reached or exceeded the total turns
        return currentTurnIndex.Value >= totalTurns;
    }

    private bool IsCurrentPlayerTurn(string playerID)
    {
        return GetCurrentPlayerIDs().Contains(playerID);
    }

    private int GetPlayerIndex(string playerID)
    {
        return playerIDs.IndexOf(playerID);
    }

    private List<string> GetCurrentPlayerIDs()
    {
        if (currentDraftPhase.Value == DraftPhase.Banning)
        {
            return new List<string> { GetBanTurnPlayer() };
        }
        else if (currentDraftPhase.Value == DraftPhase.Picking)
        {
            return GetPickTurnPlayers();
        }

        return new List<string>();
    }

    private void UpdateUIForCurrentTurn()
    {
        // Update the client UI for the current player's turn
        UpdatePlayerIcons();

        bool isCurrentPlayer = IsCurrentPlayerTurn(LobbyController.Instance.Player.Id);
        DraftPlayerState currentPlayerState = (DraftPlayerState)playerStates[GetPlayerIndex(LobbyController.Instance.Player.Id)];
        confirmButton.gameObject.SetActive(isCurrentPlayer && currentDraftPhase.Value != DraftPhase.Preparation && (currentPlayerState == DraftPlayerState.Banning || currentPlayerState == DraftPlayerState.Picking));
        switchButton.gameObject.SetActive(!isCurrentPlayer && currentDraftPhase.Value == DraftPhase.Picking && (currentTurnIndex.Value % 2 == 0) == LobbyController.Instance.GetLocalPlayerTeam());
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

    private void UpdateTimerUI(float previousValue, float newValue)
    {
        draftTimerUI.UpdateTimer(newValue);
    }

    private void UpdatePlayerIcons()
    {
        for (int i = 0; i < playerIDs.Count; i++)
        {
            string playerID = playerIDs[i].ToString();
            DraftPlayerState state = (DraftPlayerState)playerStates[i];

            foreach (var playerIcon in draftPlayerHolderBlue.PlayerIcons)
            {
                if (playerIcon.AssignedPlayer.Id == playerID)
                {
                    playerIcon.UpdateIconState(state);
                    break;
                }
            }

            foreach (var playerIcon in draftPlayerHolderOrange.PlayerIcons)
            {
                if (playerIcon.AssignedPlayer.Id == playerID)
                {
                    playerIcon.UpdateIconState(state);
                    break;
                }
            }
        }
    }

    private void StartMatch()
    {
        Debug.Log("Draft phase complete! Starting match...");
        ShowLoadingScreenRpc();
        LobbyController.Instance.LoadGameMap();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowLoadingScreenRpc()
    {
        AudioManager.StopMusic(DefaultAudioMusic.ChoosePokemon);
        AudioManager.PlayMusic(DefaultAudioMusic.LoadingTheme, true);
        AudioManager.PlaySound(DefaultAudioSounds.Play_Load17051302578487348);
        LoadingScreen.Instance.ShowMatchLoadingScreen();

        if (characterSelectModelHandle.IsValid())
        {
            Addressables.Release(characterSelectModelHandle);
        }
    }

    private void Update()
    {
        if (IsServer && draftTimer.Value > 0)
        {
            draftTimer.Value -= Time.deltaTime;
        }

        if (draftTimer.Value <= 0 && IsServer)
        {
            if (currentDraftPhase.Value == DraftPhase.Banning || currentDraftPhase.Value == DraftPhase.Picking)
            {
                HandleTimeout();
            }
            else if (currentDraftPhase.Value == DraftPhase.Preparation)
            {
                currentDraftPhase.Value = DraftPhase.Starting;
                StartMatch();
            }
        }
    }

    private void HandleTimeout()
    {
        // Get the current player IDs (since there can be two players picking)
        List<string> currentPlayerIDs = GetCurrentPlayerIDs();

        foreach (string playerID in currentPlayerIDs)
        {
            int playerIndex = GetPlayerIndex(playerID);

            // If the player hasn't confirmed, auto-confirm the selection
            if (playerIndex != -1 && playerStates[playerIndex] != (byte)DraftPlayerState.Confirmed)
            {
                // Auto-confirm the current selection or perform other timeout actions
                bool isPlayerOrangeTeam = !IsPlayerOnYourTeam(playerID);

                DraftPlayerIcon playerIcon = isPlayerOrangeTeam
                    ? draftPlayerHolderOrange.PlayerIcons.First(icon => icon.AssignedPlayer.Id == playerID)
                    : draftPlayerHolderBlue.PlayerIcons.First(icon => icon.AssignedPlayer.Id == playerID);

                int noSelectionCharacterID = currentDraftPhase.Value == DraftPhase.Banning ? -1 : CharactersList.Instance.GetCharacterID(GetRandomValidCharacter());
                CharacterInfo selectedCharacter = currentDraftPhase.Value == DraftPhase.Banning ? playerIcon.BannedCharacter : playerIcon.SelectedCharacter;

                int selectedCharacterID = selectedCharacter == null ? noSelectionCharacterID : CharactersList.Instance.GetCharacterID(selectedCharacter);

                OnPlayerConfirmed?.Invoke(playerID, selectedCharacterID);
                ConfirmSelectionRPC(playerID, selectedCharacterID);
            }
        }
    }

    private CharacterInfo GetRandomValidCharacter()
    {
        List<CharacterInfo> availableCharacters = CharactersList.Instance.Characters.Where(character => !bannedCharacters.Contains(CharactersList.Instance.GetCharacterID(character))).ToList();

        bool isValid = false;
        int index = 0;

        while (!isValid)
        {
            index = UnityEngine.Random.Range(0, availableCharacters.Count);
            isValid = true;

            foreach (var player in LobbyController.Instance.Lobby.Players)
            {
                if (player.Data.ContainsKey("SelectedCharacter"))
                {
                    if (NumberEncoder.FromBase64<short>(player.Data["SelectedCharacter"].Value) == CharactersList.Instance.GetCharacterID(availableCharacters[index]))
                    {
                        isValid = false;
                        break;
                    }
                }
            }
        }

        return availableCharacters[index];
    }

    private List<string> GetPickTurnPlayers()
    {
        List<string> currentTurnPlayers = new List<string>();

        // First pick by the blue team
        if (currentTurnIndex.Value == 0)
        {
            currentTurnPlayers.Add(blueTeamPlayers[0].Id); // Blue Team first pick
        }
        else if (currentTurnIndex.Value < blueTeamPlayers.Count*2)
        {
            int currentPlayerIndex = currentTurnIndex.Value / 2;

            // Alternate picks between teams, allowing 2 players to pick simultaneously
            if (currentTurnIndex.Value % 2 == 0)
            {
                currentTurnPlayers.Add(blueTeamPlayers[currentPlayerIndex].Id);
                if (currentPlayerIndex + 1 < blueTeamPlayers.Count)
                {
                    currentTurnPlayers.Add(blueTeamPlayers[currentPlayerIndex + 1].Id);
                }
            }
            else
            {
                currentTurnPlayers.Add(orangeTeamPlayers[currentPlayerIndex].Id);
                if (currentPlayerIndex + 1 < orangeTeamPlayers.Count)
                {
                    currentTurnPlayers.Add(orangeTeamPlayers[currentPlayerIndex + 1].Id);
                }
            }
        }

        return currentTurnPlayers;
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

    private void OnCharacterSelected(CharacterInfo info)
    {
        string playerID = LobbyController.Instance.Player.Id;
        int selectedCharacter = CharactersList.Instance.GetCharacterID(info);

        if (IsCurrentPlayerTurn(playerID))
        {
            // Send selection to the server for validation and progression
            ConfirmSelectionRPC(playerID, selectedCharacter);
        }
    }

    private void OnSwitchButtonClicked()
    {

    }

    private void UpdateHoveredCharacter()
    {
        if (currentDraftPhase.Value == DraftPhase.Banning)
        {
            if (!IsCurrentPlayerTurn(LobbyController.Instance.Player.Id))
            {
                return;
            }

            UpdateHoveredBanCharacterRPC(LobbyController.Instance.Player.Id, CharactersList.Instance.GetCharacterID(draftCharacterSelector.HoveredCharacter));
        }
        else if (currentDraftPhase.Value == DraftPhase.Picking)
        {
            if (playerStates[GetPlayerIndex(LobbyController.Instance.Player.Id)] == (byte)DraftPlayerState.Confirmed)
            {
                return;
            }

            UpdateHoveredSelectedCharacterRPC(LobbyController.Instance.Player.Id, CharactersList.Instance.GetCharacterID(draftCharacterSelector.HoveredCharacter));
        }
    }

    [Rpc(SendTo.Everyone)]
    private void UpdateHoveredBanCharacterRPC(string playerID, short selectedCharacter)
    {
        foreach (var playerIcon in draftPlayerHolderBlue.PlayerIcons)
        {
            if (playerIcon.AssignedPlayer.Id == playerID)
            {
                playerIcon.UpdateBannedCharacter(CharactersList.Instance.GetCharacterFromID(selectedCharacter));
                break;
            }
        }

        foreach (var playerIcon in draftPlayerHolderOrange.PlayerIcons)
        {
            if (playerIcon.AssignedPlayer.Id == playerID)
            {
                playerIcon.UpdateBannedCharacter(CharactersList.Instance.GetCharacterFromID(selectedCharacter));
                break;
            }
        }
    }

    [Rpc(SendTo.Everyone)]
    private void UpdateHoveredSelectedCharacterRPC(string playerID, short selectedCharacter)
    {
        foreach (var playerIcon in draftPlayerHolderBlue.PlayerIcons)
        {
            if (playerIcon.AssignedPlayer.Id == playerID)
            {
                playerIcon.UpdateSelectedCharacter(CharactersList.Instance.GetCharacterFromID(selectedCharacter));
                break;
            }
        }

        if (!IsPlayerOnYourTeam(playerID) && playerStates[GetPlayerIndex(playerID)] == (byte)DraftPlayerState.Picking)
        {
            foreach (var playerIcon in draftPlayerHolderOrange.PlayerIcons)
            {
                if (playerIcon.AssignedPlayer.Id == playerID)
                {
                    playerIcon.UpdateSelectedCharacter(CharactersList.Instance.GetCharacterFromID(selectedCharacter));
                    break;
                }
            }
        }
    }

    private void UpdatePlayerSelectedCharacter(string playerID, short selectedCharacter)
    {
        foreach (var playerIcon in draftPlayerHolderBlue.PlayerIcons)
        {
            if (playerIcon.AssignedPlayer.Id == playerID)
            {
                playerIcon.UpdateSelectedCharacter(CharactersList.Instance.GetCharacterFromID(selectedCharacter));
                break;
            }
        }

        foreach (var playerIcon in draftPlayerHolderOrange.PlayerIcons)
        {
            if (playerIcon.AssignedPlayer.Id == playerID)
            {
                playerIcon.UpdateSelectedCharacter(CharactersList.Instance.GetCharacterFromID(selectedCharacter));
                break;
            }
        }
    }

    private bool IsPlayerOnYourTeam(string playerID)
    {
        return AllyTeamPlayers.Any(player => player.Id == playerID);
    }

    private void SpawnPokemon(CharacterInfo character)
    {
        if (character == null)
        {
            return;
        }

        if (currentPokemon != null)
        {
            Destroy(currentPokemon);
            currentPokemon = null;
        }

        // Release the previous addressable handle if valid
        if (characterSelectModelHandle.IsValid())
        {
            Addressables.Release(characterSelectModelHandle);
        }

        background.SetActive(true);

        characterSelectModelHandle = Addressables.LoadAssetAsync<GameObject>(character.model);
        characterSelectModelHandle.Completed += OnCharacterSelectModelLoaded;
    }

    private void OnCharacterSelectModelLoaded(AsyncOperationHandle<GameObject> obj)
    {
        if (obj.Status == AsyncOperationStatus.Succeeded)
        {
            currentPokemon = Instantiate(obj.Result, pokemonSpawnPoint);
            background.SetActive(false);
        }
        else
        {
            Debug.LogError("Failed to load asset.");
        }
    }
}
