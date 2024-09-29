using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using JSAM;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;
using DG.Tweening;

public enum CharacterSelectPhase : byte { Selection, Preview }

public class CharacterSelectController : NetworkBehaviour
{
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private const float SELECTION_TIME = 10f;
    private const float PREVIEW_TIME = 5f;
#else
    private const float SELECTION_TIME = 30f;
    private const float PREVIEW_TIME = 20f;
#endif
    private const bool ALLOW_DUPLICATE_CHARACTERS = false;

    [SerializeField] private GameObject playerIconPrefab;
    [SerializeField] private Transform playerIconsHolder;

    [SerializeField] CharacterSelectorUI characterSelectorUI;

    [SerializeField] private GameObject mainScreen;
    [SerializeField] private PreviewScreenUI previewScreenUI;
    [SerializeField] private PkmnListSideInfo pokemonInfo;

    [SerializeField] private Transform pokemonSpawnPoint;
    private GameObject currentPokemon;

    [SerializeField] private TMP_Text timerText;

    [SerializeField] private TrainerModel trainerModel;

    private NetworkVariable<float> selectionTimer = new NetworkVariable<float>(SELECTION_TIME);
    private NetworkVariable<CharacterSelectPhase> currentPhase = new NetworkVariable<CharacterSelectPhase>(CharacterSelectPhase.Selection);
    private bool isLoading = false;
    private bool startTimer = false;
    private bool hasSelectedCharacter = false;

    private int lastValue = 0;

    private AsyncOperationHandle<GameObject> characterSelectModelHandle;

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
        if (sceneName == "CharacterSelect")
        {
            if (IsServer)
            {
                selectionTimer.Value = SELECTION_TIME;
                startTimer = true;
                currentPhase.Value = CharacterSelectPhase.Selection;
            }
            LoadingScreen.Instance.HideGameBeginScreen();
        }
    }

    public override void OnNetworkSpawn()
    {
        AudioManager.StopMusic(DefaultAudioMusic.LobbyTheme);
        AudioManager.PlayMusic(DefaultAudioMusic.ChoosePokemon, true);

        Player[] localTeamPlayers = LobbyController.Instance.GetTeamPlayers(LobbyController.Instance.GetLocalPlayerTeam());
        foreach (var player in localTeamPlayers)
        {
            GameObject playerIcon = Instantiate(playerIconPrefab, playerIconsHolder);
            playerIcon.GetComponent<PlayerSelectionIcon>().Initialize(player);
            if (player.Id == LobbyController.Instance.Player.Id && !string.IsNullOrEmpty(player.Data["SelectedCharacter"].Value))
            {
                SpawnPokemon(CharactersList.Instance.GetCharacterFromID(NumberEncoder.FromBase64<short>(player.Data["SelectedCharacter"].Value)));
            }
        }

        List<CharacterSelectIcon> characterSelectIcons = characterSelectorUI.InitializeUI();

        foreach (var icon in characterSelectIcons)
        {
            icon.OnCharacterSelected += ChangeCharacter;
        }

        selectionTimer.OnValueChanged += UpdateTimerText;

        currentPhase.OnValueChanged += (previous, current) =>
        {
            if (current == CharacterSelectPhase.Preview)
            {
                mainScreen.SetActive(false);
                previewScreenUI.gameObject.SetActive(true);
                previewScreenUI.InitializeUI();
                AudioManager.PlaySound(DefaultAudioSounds.Home_ui_dressingroom);
            }
        };

        mainScreen.SetActive(true);
        previewScreenUI.gameObject.SetActive(false);
        pokemonInfo.gameObject.SetActive(false);

        trainerModel.InitializeClothes(PlayerClothesInfo.Deserialize(LobbyController.Instance.Player.Data["ClothingInfo"].Value));
    }

    private void UpdateTimerText(float previous, float current)
    {
        int time = Mathf.FloorToInt(current);
        if (time < 0)
        {
            time = 0;
        }

        switch (currentPhase.Value)
        {
            case CharacterSelectPhase.Selection:
                timerText.text = time.ToString();

                if (time <= 15 && time != lastValue)
                {
                    timerText.color = new Color(255 / 255f, 111 / 255f, 6 / 255f, 1f);
                    AudioManager.PlaySound(DefaultAudioSounds.Play_UI_Countdown);
                    PlayShadowEffect();
                    PlayPopInAnimation();
                }
                else if (lastValue != time)
                {
                    timerText.color = Color.black;
                }
                break;
            case CharacterSelectPhase.Preview:
                previewScreenUI.UpdateTimerValue(time);
                break;
            default:
                break;
        }

        if (time != lastValue)
        {
            lastValue = time;
        }
    }

    void PlayPopInAnimation()
    {
        timerText.transform.localScale = Vector3.one;

        timerText.transform.DOScale(new Vector3(1.2f, 1.2f, 1.2f), 0.15f)
            .SetEase(Ease.OutBack)
            .OnComplete(() => timerText.transform.DOScale(Vector3.one, 0.1f));
    }

    void PlayShadowEffect()
    {
        TMP_Text shadowText = Instantiate(timerText, timerText.transform.parent);

        shadowText.color = timerText.color;
        shadowText.transform.localScale = timerText.transform.localScale;

        shadowText.transform.DOScale(new Vector3(1.9f, 1.9f, 1.9f), 0.6f);
        shadowText.DOFade(0, 0.6f).OnComplete(() => Destroy(shadowText.gameObject));
    }

    private void Update()
    {
        if (IsServer)
        {
            if (startTimer)
            {
                selectionTimer.Value -= Time.deltaTime;
                if (selectionTimer.Value <= 0 && !isLoading)
                {
                    switch (currentPhase.Value)
                    {
                        case CharacterSelectPhase.Selection:
                            currentPhase.Value = CharacterSelectPhase.Preview;
                            selectionTimer.Value = PREVIEW_TIME;
                            break;
                        case CharacterSelectPhase.Preview:
                            if (!isLoading)
                            {
                                ShowLoadingScreenRpc();
                                LobbyController.Instance.LoadGameMap();
                                isLoading = true;
                            }
                            break;
                        default:
                            break;
                    }

                }
            }
        }

        if (selectionTimer.Value <= 0.3f && !hasSelectedCharacter)
        {
            if (string.IsNullOrEmpty(LobbyController.Instance.Player.Data["SelectedCharacter"].Value))
            {
                ChangeCharacter(GetRandomCharacter());
            }
        }
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

    private void ChangeCharacter(CharacterInfo character)
    {
        if (!IsCharacterAvailable(character.pokemonName) && !ALLOW_DUPLICATE_CHARACTERS)
        {
            return;
        }

        hasSelectedCharacter = true;
        LobbyController.Instance.ChangePlayerCharacter(CharactersList.Instance.GetCharacterID(character));
        pokemonInfo.gameObject.SetActive(true);
        pokemonInfo.SetPokemonInfo(character);
        SpawnPokemon(character);
    }

    private bool IsCharacterAvailable(string characterName)
    {
        Player[] localTeamPlayers = LobbyController.Instance.GetTeamPlayers(LobbyController.Instance.GetLocalPlayerTeam());
        foreach (var player in localTeamPlayers)
        {
            if (player.Id != LobbyController.Instance.Player.Id)
            {
                if (player.Data["SelectedCharacter"].Value == characterName)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private CharacterInfo GetRandomCharacter()
    {
        bool found = false;

        CharacterInfo character = null;

        while (!found)
        {
            character = CharactersList.Instance.Characters[UnityEngine.Random.Range(0, CharactersList.Instance.Characters.Length)];
            if (IsCharacterAvailable(character.pokemonName))
            {
                found = true;
            }
        }

        return character;
    }

    private void SpawnPokemon(CharacterInfo character)
    {
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

        characterSelectModelHandle = Addressables.LoadAssetAsync<GameObject>(character.model);
        characterSelectModelHandle.Completed += OnCharacterSelectModelLoaded;
    }

    private void OnCharacterSelectModelLoaded(AsyncOperationHandle<GameObject> obj)
    {
        if (obj.Status == AsyncOperationStatus.Succeeded)
        {
            currentPokemon = Instantiate(obj.Result, pokemonSpawnPoint);
        }
        else
        {
            Debug.LogError("Failed to load asset.");
        }
    }
}
