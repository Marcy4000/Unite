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

private Dictionary<Team, List<LobbyPlayerIcon>> teamPlayerIcons = new Dictionary<Team, List<LobbyPlayerIcon>>();

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

        var teams = selectedMap.availableTeams;
        int slotsPerTeam = selectedMap.maxTeamSize;

        teamPlayerIcons.Clear();

        foreach (var team in teams)
        {
            var iconList = new List<LobbyPlayerIcon>();
            for (int i = 0; i < slotsPerTeam; i++)
            {
                var icon = Instantiate(playerIconPrefab, playerIconHolder.transform).GetComponent<LobbyPlayerIcon>();
                icon.InitializeElement(team, (short)i);
                iconList.Add(icon);
            }
            teamPlayerIcons[team] = iconList;
        }

        // Add listeners after all icons are created
        foreach (var kvp in teamPlayerIcons)
        {
            var team = kvp.Key;
            var iconList = kvp.Value;
            for (int i = 0; i < iconList.Count; i++)
            {
                int index = i;
                iconList[index].SwitchButton.onClick.AddListener(() => CheckIfPosIsAvailable(iconList[index]));
                iconList[index].KickButton.onClick.AddListener(() => LobbyController.Instance.KickPlayer(iconList[index].PlayerId));
            }
        }

        lobbyCodeText.text = $"Lobby Code: {lobby.LobbyCode}";
        startGameButton.gameObject.SetActive(lobby.HostId == Unity.Services.Authentication.AuthenticationService.Instance.PlayerId);
        openLobbyToggle.interactable = lobby.HostId == Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;

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

        var teams = selectedMap.availableTeams;
        int slotsPerTeam = selectedMap.maxTeamSize;

        foreach (var iconList in teamPlayerIcons.Values)
        {
            foreach (var icon in iconList)
            {
                icon.ResetName();
            }
        }

        foreach (var player in lobby.Players)
        {
            try
            {
                int playerPos = NumberEncoder.FromBase64<short>(player.Data["PlayerPos"].Value);
                Team team = TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value);
                if (!teamPlayerIcons.ContainsKey(team)) continue;
                if (playerPos < 0 || playerPos >= teamPlayerIcons[team].Count)
                {
                    Debug.LogWarning($"PlayerPos {playerPos} out of bounds for team {team} (slotsPerTeam={slotsPerTeam})");
                    continue;
                }
                teamPlayerIcons[team][playerPos].InitializePlayer(player);
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
                var teams = selectedMap.availableTeams;
                var teamCounts = new Dictionary<Team, int>();
                foreach (var team in teams)
                    teamCounts[team] = 0;
                foreach (var player in LobbyController.Instance.Lobby.Players)
                {
                    Team t = TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value);
                    if (teamCounts.ContainsKey(t))
                        teamCounts[t]++;
                }
                int? refCount = null;
                foreach (var count in teamCounts.Values)
                {
                    if (refCount == null)
                        refCount = count;
                    else if (count != refCount)
                        return;
                }
                LobbyController.Instance.StartGame();
                break;
            case StartRestriction.FullTeams:
                var teamsFull = selectedMap.availableTeams;
                var teamCountsFull = new Dictionary<Team, int>();
                foreach (var team in teamsFull)
                    teamCountsFull[team] = 0;
                foreach (var player in LobbyController.Instance.Lobby.Players)
                {
                    Team t = TeamMember.GetTeamFromString(player.Data["PlayerTeam"].Value);
                    if (teamCountsFull.ContainsKey(t))
                        teamCountsFull[t]++;
                }
                foreach (var count in teamCountsFull.Values)
                {
                    if (count != selectedMap.maxTeamSize)
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

    public void ChangePosition(Team team, short position)
    {
        LobbyController.Instance.UpdatePlayerTeamAndPos(team, position);
    }

    private void CheckIfPosIsAvailable(LobbyPlayerIcon playerIcon)
    {
        var team = playerIcon.Team;
        var pos = playerIcon.Position;
        if (teamPlayerIcons.ContainsKey(team) && teamPlayerIcons[team][pos].PlayerName == "No Player")
        {
            ChangePosition(team, pos);
        }
    }

    public void ClearUI()
    {
        foreach (var iconList in teamPlayerIcons.Values)
        {
            foreach (var icon in iconList)
            {
                if (icon != null)
                    Destroy(icon.gameObject);
            }
        }
        teamPlayerIcons.Clear();
    }
}
