using JSAM;
using TMPro;
using Unity.Services.Authentication;
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
    [SerializeField] private int maxPlayers = 10;

    [SerializeField] private MapSelector mapSelector;

    private LobbyPlayerIcon[] playerIconsBlueTeam;
    private LobbyPlayerIcon[] playerIconsOrangeTeam;

    private MapInfo selectedMap;

    private void Start()
    {
        openLobbyToggle.onValueChanged.AddListener((value) => LobbyController.Instance.ChangeLobbyVisibility(!value));
    }

    private void OnEnable()
    {
        LobbyController.Instance.onLobbyUpdate += UpdatePlayers;
        LobbyController.Instance.onLobbyUpdate += OnLobbyUpdate;
    }

    private void OnDisable()
    {
        LobbyController.Instance.onLobbyUpdate -= UpdatePlayers;
        LobbyController.Instance.onLobbyUpdate -= OnLobbyUpdate;
    }

    private void OnLobbyUpdate(Lobby lobby)
    {
        openLobbyToggle.isOn = !lobby.IsPrivate;
        mapSelector.UpdateSelectedMap();
        if (CharactersList.Instance.GetCurrentLobbyMap() != selectedMap)
        {
            AudioManager.PlaySound(DefaultAudioSounds.Play_UI_MapSwitch);
            selectedMap = CharactersList.Instance.GetCurrentLobbyMap();
            LobbyController.Instance.CheckIfShouldChangePos(selectedMap.maxTeamSize);
            InitializeUI(LobbyController.Instance.Lobby);
        }
    }

    private void Update()
    {
        startGameButton.interactable = !LobbyController.Instance.IsAnyPlayerInResultScreen();
    }

    public void InitializeUI(Lobby lobby)
    {
        ClearUI();

        openLobbyToggle.isOn = !lobby.IsPrivate;

        mapSelector.Initialize(lobby.HostId == AuthenticationService.Instance.PlayerId);
        selectedMap = CharactersList.Instance.GetCurrentLobbyMap();

        maxPlayers = selectedMap.maxTeamSize*2;

        playerIconsBlueTeam = new LobbyPlayerIcon[maxPlayers/2];
        playerIconsOrangeTeam = new LobbyPlayerIcon[maxPlayers/2];
        lobbyCodeText.text = $"Lobby Code: {lobby.LobbyCode}";

        startGameButton.gameObject.SetActive(lobby.HostId == AuthenticationService.Instance.PlayerId);
        openLobbyToggle.interactable = lobby.HostId == AuthenticationService.Instance.PlayerId;

        for (int i = 0; i < maxPlayers; i++)
        {
            if (i < maxPlayers/2)
            {
                int index = i;
                playerIconsBlueTeam[i] = Instantiate(playerIconPrefab, playerIconHolder.transform).GetComponent<LobbyPlayerIcon>();
                playerIconsBlueTeam[i].InitializeElement(false, (short)i);
                playerIconsBlueTeam[i].SwitchButton.onClick.AddListener(() => CheckIfPosIsAvailable(playerIconsBlueTeam[index]));
            }
            else
            {
                int index = i - maxPlayers/2;
                playerIconsOrangeTeam[index] = Instantiate(playerIconPrefab, playerIconHolder.transform).GetComponent<LobbyPlayerIcon>();
                playerIconsOrangeTeam[index].InitializeElement(true, (short)index);
                playerIconsOrangeTeam[index].SwitchButton.onClick.AddListener(() => CheckIfPosIsAvailable(playerIconsOrangeTeam[index]));
                playerIconsOrangeTeam[index].KickButton.onClick.AddListener(() => LobbyController.Instance.KickPlayer(playerIconsOrangeTeam[index].PlayerName));
            }
        }

        UpdatePlayers(lobby);
    }

    public void UpdatePlayers(Lobby lobby)
    {
        startGameButton.gameObject.SetActive(lobby.HostId == AuthenticationService.Instance.PlayerId);

        for (int i = 0; i < maxPlayers; i++)
        {
            if (i < maxPlayers / 2)
            {
                playerIconsBlueTeam[i].ResetName();
            }
            else
            {
                playerIconsOrangeTeam[i - maxPlayers / 2].ResetName();
            }
        }

        for (int i = 0; i < lobby.Players.Count; i++)
        {
            try
            {
                int playerPos = NumberEncoder.FromBase64<short>(lobby.Players[i].Data["PlayerPos"].Value);
                if (lobby.Players[i].Data["PlayerTeam"].Value == "Blue")
                {
                    playerIconsBlueTeam[playerPos].InitializePlayer(lobby.Players[i]);
                }
                else
                {
                    playerIconsOrangeTeam[playerPos].InitializePlayer(lobby.Players[i]);
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
        LobbyController.Instance.StartGame();
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
        if (playerIconsBlueTeam == null || playerIconsOrangeTeam == null)
        {
            return;
        }

        for (int i = 0; i < maxPlayers; i++)
        {
            if (i < maxPlayers / 2)
            {
                Destroy(playerIconsBlueTeam[i].gameObject);
            }
            else
            {
                Destroy(playerIconsOrangeTeam[i - maxPlayers/2].gameObject);
            }
        }

        playerIconsBlueTeam = null;
        playerIconsOrangeTeam = null;
    }
}
