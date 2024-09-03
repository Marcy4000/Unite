using JSAM;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button startLobbyButton;
    [SerializeField] private Button exitLobbyButton;
    [SerializeField] private TMP_InputField lobbyIdField;

    [SerializeField] private GameObject[] lobbyUIs;
    [SerializeField] private GameObject[] lobbyScenes;
    [SerializeField] private PartyScreenUI partyScreenUI;
    [SerializeField] private LobbyPlayerInfoIcons lobbyPlayerInfoIcons;

    [SerializeField] private TrainerModel trainerModel;

    [SerializeField] private Dialogue[] clothesMenuDialogue;

    private bool showedMessage = false;

    private void Start()
    {
        startLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySound(DefaultAudioSounds.Home_ui_start_04);
            LobbyController.Instance.CreateLobby();
        });

        lobbyIdField.onEndEdit.AddListener((string lobbyId) =>
        {
            if (string.IsNullOrWhiteSpace(lobbyId))
            {
                return;
            }
            LobbyController.Instance.TryLobbyJoin(lobbyId.ToUpper());
        });

        exitLobbyButton.onClick.AddListener(() =>
        {
            AudioManager.PlaySound(DefaultAudioSounds.Home_ui_back_01);
            LobbyController.Instance.LeaveLobby();
        });

        lobbyPlayerInfoIcons.Initialize(LobbyController.Instance.Player);

        AudioManager.PlayMusic(DefaultAudioMusic.LobbyTheme, true);

        ShowMainMenuUI();
    }

    public void ShowPlayerMenuUI()
    {
        lobbyUIs[2].SetActive(true);
    }

    public void ShowLobbyUI()
    {
        foreach (var lobbyUI in lobbyUIs)
        {
            lobbyUI.SetActive(false);
        }

        foreach (var lobbyScene in lobbyScenes)
        {
            lobbyScene.SetActive(false);
        }

        lobbyUIs[1].SetActive(true);
        lobbyScenes[1].SetActive(true);
        partyScreenUI.InitializeUI(LobbyController.Instance.Lobby);
    }

    public void ShowMainMenuUI()
    {
        foreach (var lobbyUI in lobbyUIs)
        {
            lobbyUI.SetActive(false);
        }

        foreach (var lobbyScene in lobbyScenes)
        {
            lobbyScene.SetActive(false);
        }

        lobbyUIs[0].SetActive(true);
        lobbyScenes[0].SetActive(true);
        trainerModel.InitializeClothes(PlayerClothesInfo.Deserialize(LobbyController.Instance.Player.Data["ClothingInfo"].Value));
    }

    public void ShowClothesMenu()
    {
        foreach (var lobbyUI in lobbyUIs)
        {
            lobbyUI.SetActive(false);
        }

        foreach (var lobbyScene in lobbyScenes)
        {
            lobbyScene.SetActive(false);
        }

        lobbyUIs[3].SetActive(true);
        if (!showedMessage)
        {
            DialogueSystem.Instance.SetDialogues(clothesMenuDialogue);
            showedMessage = true;
        }
    }

    public void ShowPokemonMenu()
    {
        foreach (var lobbyUI in lobbyUIs)
        {
            lobbyUI.SetActive(false);
        }

        foreach (var lobbyScene in lobbyScenes)
        {
            lobbyScene.SetActive(false);
        }

        lobbyUIs[4].SetActive(true);
    }

    public void UpdatePartyScreenUI()
    {
        partyScreenUI.UpdatePlayers(LobbyController.Instance.Lobby);
    }
}
