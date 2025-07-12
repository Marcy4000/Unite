using JSAM;
using TMPro;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class PartyScreenUI : MonoBehaviour
{
    [SerializeField] private GameObject playerIconPrefab;
    [SerializeField] private GameObject playerIconHolder;
    [SerializeField] private Button startGameButton;
    [SerializeField] private TMP_Text lobbyCodeText;
    [SerializeField] private Toggle openLobbyToggle;
    [SerializeField] private Toggle customLobbyToggle;
    [SerializeField] private Toggle standardsLobbyToggle;
    [SerializeField] private int maxPlayers = 10;

    [SerializeField] private MapSelector mapSelector;

    [SerializeField] private GameObject standardsLobbyRoot; // UI alternativa per standards (assegnala da Inspector)
    [SerializeField] private GameObject customsLobbyRoot;   // Container per la UI custom (assegnala da Inspector)

    [SerializeField] private PartyPlayerModel[] standardsLobbyPlayers;

    [Header("Matchmaking UI Control")]
    [SerializeField] private Button[] disableOnMatchmaking;
    [SerializeField] private Toggle[] disableTogglesOnMatchmaking;
    [SerializeField] private GameObject[] hideOnMatchmaking;

    private List<LobbyPlayerIcon> playerIconsBlueTeam;
    private List<LobbyPlayerIcon> playerIconsOrangeTeam;

    private MapInfo selectedMap;

    private LobbyController.LobbyType? lastLobbyType = null;
    private bool lastMatchmakingState = false;

    private void Start()
    {
        openLobbyToggle.onValueChanged.AddListener((value) => LobbyController.Instance.ChangeLobbyVisibility(!value));
        customLobbyToggle.onValueChanged.AddListener(OnCustomLobbyToggleChanged);
        standardsLobbyToggle.onValueChanged.AddListener(OnStandardsLobbyToggleChanged);
    }

    private void OnCustomLobbyToggleChanged(bool isOn)
    {
        if (isOn && !LobbyController.Instance.IsCustomLobby() && IsLocalPlayerHost())
        {
            LobbyController.Instance.ChangeLobbyType(LobbyController.LobbyType.Custom);
        }
    }

    private void OnStandardsLobbyToggleChanged(bool isOn)
    {
        if (isOn && !LobbyController.Instance.IsStandardsLobby() && IsLocalPlayerHost())
        {
            LobbyController.Instance.ChangeLobbyType(LobbyController.LobbyType.Standards);
        }
    }

    private bool IsLocalPlayerHost()
    {
        return LobbyController.Instance.Lobby != null &&
               LobbyController.Instance.Lobby.HostId == Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
    }

    private void OnEnable()
    {
        LobbyController.Instance.onLobbyUpdate += UpdatePlayers;
        LobbyController.Instance.onLobbyUpdate += OnLobbyUpdate;
        UpdateLobbyTypeToggles();
    }

    private void OnDisable()
    {
        LobbyController.Instance.onLobbyUpdate -= UpdatePlayers;
        LobbyController.Instance.onLobbyUpdate -= OnLobbyUpdate;
    }

    private void OnLobbyUpdate(Lobby lobby)
    {
        openLobbyToggle.isOn = !lobby.IsPrivate;
        UpdateLobbyTypeToggles();
        mapSelector.UpdateSelectedMap();

        var currentType = LobbyController.Instance.IsCustomLobby() ? LobbyController.LobbyType.Custom : LobbyController.LobbyType.Standards;
        if (lastLobbyType == null || lastLobbyType != currentType)
        {
            lastLobbyType = currentType;
            InitializeUI(lobby);
            return;
        }

        if (CharactersList.Instance.GetCurrentLobbyMap() != selectedMap)
        {
            AudioManager.PlaySound(DefaultAudioSounds.Play_UI_MapSwitch);
            selectedMap = CharactersList.Instance.GetCurrentLobbyMap();
            LobbyController.Instance.CheckIfShouldChangePos(selectedMap.maxTeamSize);
            InitializeUI(LobbyController.Instance.Lobby);
        }
    }

    private void UpdateLobbyTypeToggles()
    {
        // Aggiorna lo stato dei radio button in base al tipo di lobby corrente
        bool isCustom = LobbyController.Instance.IsCustomLobby();
        bool isStandards = LobbyController.Instance.IsStandardsLobby();

        // Disabilita la modifica se non sei host
        bool interactable = IsLocalPlayerHost();
        customLobbyToggle.interactable = interactable;
        standardsLobbyToggle.interactable = interactable;

        // Imposta il valore senza triggerare l'evento
        if (customLobbyToggle.isOn != isCustom)
            customLobbyToggle.SetIsOnWithoutNotify(isCustom);
        if (standardsLobbyToggle.isOn != isStandards)
            standardsLobbyToggle.SetIsOnWithoutNotify(isStandards);
    }

    private void Update()
    {
        startGameButton.interactable = !LobbyController.Instance.IsAnyPlayerInResultScreen();
        UpdateMatchmakingUI();
    }

    private void UpdateMatchmakingUI()
    {
        bool isMatchmaking = LobbyController.Instance.IsMatchmaking;

        // Only update UI if matchmaking state has changed
        if (isMatchmaking != lastMatchmakingState)
        {
            lastMatchmakingState = isMatchmaking;

            // Disable/enable buttons during matchmaking
            if (disableOnMatchmaking != null)
            {
                foreach (var button in disableOnMatchmaking)
                {
                    if (button != null)
                        button.interactable = !isMatchmaking;
                }
            }

            // Disable/enable toggles during matchmaking
            if (disableTogglesOnMatchmaking != null)
            {
                foreach (var toggle in disableTogglesOnMatchmaking)
                {
                    if (toggle != null)
                        toggle.interactable = !isMatchmaking;
                }
            }

            // Hide/show GameObjects during matchmaking
            if (hideOnMatchmaking != null)
            {
                foreach (var gameObject in hideOnMatchmaking)
                {
                    if (gameObject != null)
                        gameObject.SetActive(!isMatchmaking);
                }
            }

            // Also disable lobby type toggles during matchmaking
            if (isMatchmaking)
            {
                customLobbyToggle.interactable = false;
                standardsLobbyToggle.interactable = false;
                openLobbyToggle.interactable = false;
            }
            else
            {
                // Re-enable based on normal conditions when matchmaking stops
                UpdateLobbyTypeToggles();
                openLobbyToggle.interactable = IsLocalPlayerHost();
            }
        }
    }

    public void InitializeUI(Lobby lobby)
    {
        ClearUI();

        openLobbyToggle.isOn = !lobby.IsPrivate;

        mapSelector.Initialize(lobby.HostId == Unity.Services.Authentication.AuthenticationService.Instance.PlayerId);
        selectedMap = CharactersList.Instance.GetCurrentLobbyMap();

        // Aggiorna la UI in base al tipo di lobby
        bool isCustom = LobbyController.Instance.IsCustomLobby();
        customsLobbyRoot.SetActive(isCustom);
        standardsLobbyRoot.SetActive(!isCustom);

        var localPlayerId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
        int localPlayerIndex = lobby.Players.FindIndex(p => p.Id == localPlayerId);

        List<Player> otherPlayers = new List<Player>();
        for (int i = 0; i < lobby.Players.Count; i++)
        {
            if (i != localPlayerIndex)
                otherPlayers.Add(lobby.Players[i]);
        }

        for (int i = 0; i < standardsLobbyPlayers.Length; i++)
        {
            if (isCustom)
            {
                standardsLobbyPlayers[i].gameObject.SetActive(false);
            }
            else
            {
                if (i == 0 && localPlayerIndex != -1)
                {
                    standardsLobbyPlayers[i].gameObject.SetActive(true);
                    standardsLobbyPlayers[i].Initialize(lobby.Players[localPlayerIndex]);
                }
                else if (i - 1 < otherPlayers.Count)
                {
                    standardsLobbyPlayers[i].gameObject.SetActive(true);
                    standardsLobbyPlayers[i].Initialize(otherPlayers[i - 1]);
                }
                else
                {
                    standardsLobbyPlayers[i].gameObject.SetActive(false);
                }
            }
        }

        if (!isCustom)
        {
            // Standards: non mostrare icone giocatori, UI alternativa (per ora vuota)
            lobbyCodeText.text = $"Lobby Code: {lobby.LobbyCode}";
            startGameButton.gameObject.SetActive(false);
            openLobbyToggle.interactable = lobby.HostId == Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
            return;
        }

        // --- CUSTOMS LOBBY UI ---
        maxPlayers = LobbyController.Instance.GetMaxPartyMembers();

        int blueSlots = 0, orangeSlots = 0;
        foreach (var player in lobby.Players)
        {
            if (player.Data["PlayerTeam"].Value == "Blue") blueSlots++;
            else orangeSlots++;
        }
        int slotsPerTeam = Mathf.Max(maxPlayers / 2, Mathf.Max(blueSlots, orangeSlots, 1));

        if (playerIconsBlueTeam == null) playerIconsBlueTeam = new List<LobbyPlayerIcon>(slotsPerTeam);
        if (playerIconsOrangeTeam == null) playerIconsOrangeTeam = new List<LobbyPlayerIcon>(slotsPerTeam);

        lobbyCodeText.text = $"Lobby Code: {lobby.LobbyCode}";

        startGameButton.gameObject.SetActive(lobby.HostId == Unity.Services.Authentication.AuthenticationService.Instance.PlayerId);
        openLobbyToggle.interactable = lobby.HostId == Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;

        for (int i = 0; i < slotsPerTeam; i++)
        {
            var blueIcon = Instantiate(playerIconPrefab, playerIconHolder.transform).GetComponent<LobbyPlayerIcon>();
            blueIcon.InitializeElement(false, (short)i);
            // L'evento sarà aggiunto dopo che la lista è completa
            playerIconsBlueTeam.Add(blueIcon);
        }
        for (int i = 0; i < slotsPerTeam; i++)
        {
            var orangeIcon = Instantiate(playerIconPrefab, playerIconHolder.transform).GetComponent<LobbyPlayerIcon>();
            orangeIcon.InitializeElement(true, (short)i);
            // L'evento sarà aggiunto dopo che la lista è completa
            playerIconsOrangeTeam.Add(orangeIcon);
        }
        // Ora che le liste sono popolate, aggiungi i listener
        for (int i = 0; i < slotsPerTeam; i++)
        {
            int index = i;
            playerIconsBlueTeam[index].SwitchButton.onClick.AddListener(() => CheckIfPosIsAvailable(playerIconsBlueTeam[index]));
            playerIconsBlueTeam[index].KickButton.onClick.AddListener(() => LobbyController.Instance.KickPlayer(playerIconsBlueTeam[index].PlayerId));
            playerIconsOrangeTeam[index].SwitchButton.onClick.AddListener(() => CheckIfPosIsAvailable(playerIconsOrangeTeam[index]));
            playerIconsOrangeTeam[index].KickButton.onClick.AddListener(() => LobbyController.Instance.KickPlayer(playerIconsOrangeTeam[index].PlayerId));
        }

        UpdatePlayers(lobby);
    }

    public void UpdatePlayers(Lobby lobby)
    {
        bool isCustom = LobbyController.Instance.IsCustomLobby();

        startGameButton.gameObject.SetActive(lobby.HostId == Unity.Services.Authentication.AuthenticationService.Instance.PlayerId);

        if (!isCustom)
        {
            // Trova il giocatore locale
            var localPlayerId = Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
            int localPlayerIndex = lobby.Players.FindIndex(p => p.Id == localPlayerId);
            // Prepara la lista degli altri giocatori
            List<Player> otherPlayers = new List<Player>();
            for (int i = 0; i < lobby.Players.Count; i++)
            {
                if (i != localPlayerIndex)
                    otherPlayers.Add(lobby.Players[i]);
            }

            for (int i = 0; i < standardsLobbyPlayers.Length; i++)
            {
                if (isCustom)
                {
                    standardsLobbyPlayers[i].gameObject.SetActive(false);
                }
                else
                {
                    if (i == 0 && localPlayerIndex != -1)
                    {
                        standardsLobbyPlayers[i].gameObject.SetActive(true);
                        standardsLobbyPlayers[i].Initialize(lobby.Players[localPlayerIndex]);
                    }
                    else if (i - 1 < otherPlayers.Count)
                    {
                        standardsLobbyPlayers[i].gameObject.SetActive(true);
                        standardsLobbyPlayers[i].Initialize(otherPlayers[i - 1]);
                    }
                    else
                    {
                        standardsLobbyPlayers[i].gameObject.SetActive(false);
                    }
                }
            }
            return;
        }

        int slotsPerTeam = playerIconsBlueTeam.Count;

        for (int i = 0; i < slotsPerTeam; i++)
        {
            playerIconsBlueTeam[i].ResetName();
            playerIconsOrangeTeam[i].ResetName();
        }

        foreach (var player in lobby.Players)
        {
            try
            {
                int playerPos = NumberEncoder.FromBase64<short>(player.Data["PlayerPos"].Value);
                string team = player.Data["PlayerTeam"].Value;
                if (playerPos < 0 || playerPos >= slotsPerTeam)
                {
                    Debug.LogWarning($"PlayerPos {playerPos} out of bounds for team {team} (slotsPerTeam={slotsPerTeam})");
                    continue;
                }
                if (team == "Blue")
                {
                    playerIconsBlueTeam[playerPos].InitializePlayer(player);
                }
                else
                {
                    playerIconsOrangeTeam[playerPos].InitializePlayer(player);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error while updating players: {ex.Message}");
            }
        }
    }

    public void StartGame()
    {
        switch (selectedMap.startRestriction)
        {
            case StartRestriction.None:
                LobbyController.Instance.StartGame();
                break;
            case StartRestriction.SameTeamSizes:
                if (LobbyController.Instance.Lobby.Players.Count % 2 != 0)
                {
                    return;
                }

                int blueTeamCount = 0;
                int orangeTeamCount = 0;

                for (int i = 0; i < LobbyController.Instance.Lobby.Players.Count; i++)
                {
                    if (LobbyController.Instance.Lobby.Players[i].Data["PlayerTeam"].Value == "Blue")
                    {
                        blueTeamCount++;
                    }
                    else
                    {
                        orangeTeamCount++;
                    }
                }

                if (blueTeamCount != orangeTeamCount)
                {
                    return;
                }

                LobbyController.Instance.StartGame();
                break;
            case StartRestriction.FullTeams:
                if (LobbyController.Instance.Lobby.Players.Count != maxPlayers)
                {
                    return;
                }

                int blueTeamCountFull = 0;
                int orangeTeamCountFull = 0;

                for (int i = 0; i < LobbyController.Instance.Lobby.Players.Count; i++)
                {
                    if (LobbyController.Instance.Lobby.Players[i].Data["PlayerTeam"].Value == "Blue")
                    {
                        blueTeamCountFull++;
                    }
                    else
                    {
                        orangeTeamCountFull++;
                    }
                }

                if (blueTeamCountFull != maxPlayers / 2 || orangeTeamCountFull != maxPlayers / 2)
                {
                    return;
                }

                LobbyController.Instance.StartGame();
                break;
            default:
                break;
        }
    }

    public void CopyLobbyID()
    {
        GUIUtility.systemCopyBuffer = lobbyCodeText.text.Split(' ')[2];
    }

    public void SwitchTeam()
    {
        LobbyController.Instance.PlayerSwitchTeam();
    }

    public void ChangePosition(string team, short position)
    {
        LobbyController.Instance.UpdatePlayerTeamAndPos(team, position);
    }

    private void CheckIfPosIsAvailable(LobbyPlayerIcon playerIcon)
    {
        if (!playerIcon.OrangeTeam)
        {
            if (playerIconsBlueTeam[playerIcon.Position].PlayerName == "No Player")
            {
                ChangePosition("Blue", playerIcon.Position);
            }
        }
        else
        {
            if (playerIconsOrangeTeam[playerIcon.Position].PlayerName == "No Player")
            {
                ChangePosition("Orange", playerIcon.Position);
            }
        }
    }

    public void ClearUI()
    {
        if (playerIconsBlueTeam != null)
        {
            foreach (var icon in playerIconsBlueTeam)
            {
                if (icon != null)
                    Destroy(icon.gameObject);
            }
            playerIconsBlueTeam.Clear();
        }
        if (playerIconsOrangeTeam != null)
        {
            foreach (var icon in playerIconsOrangeTeam)
            {
                if (icon != null)
                    Destroy(icon.gameObject);
            }
            playerIconsOrangeTeam.Clear();
        }
    }
}
