using JSAM;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public sealed class OpenWorldLobbyView : MonoBehaviour
{
    public const string MapSceneKey = "Assets/Scenes/ChineseLobbyMap.unity";
    private AsyncOperationHandle<SceneInstance> mapHandle;

    public async Task LoadMapAsync()
    {
        mapHandle = Addressables.LoadSceneAsync(MapSceneKey, LoadSceneMode.Additive);
        await mapHandle.Task;
        if (mapHandle.Status != AsyncOperationStatus.Succeeded)
            throw new System.InvalidOperationException("Could not load the open world lobby map.");
    }

    private void OnDestroy()
    {
        if (!mapHandle.IsValid()) return;
        if (mapHandle.Status == AsyncOperationStatus.Succeeded)
            Addressables.UnloadSceneAsync(mapHandle);
        else
            Addressables.Release(mapHandle);
    }

    [SerializeField] private OpenWorldTrainerAvatar avatarPrefab;
    [SerializeField] private Vector3 spawnCenter = new Vector3(0f, 0.3f, 0f);
    [SerializeField] private TMP_Text status;
    [SerializeField] private GameObject mobileControls;
    [SerializeField] private EventSystem lobbyEventSystem;
    public OpenWorldTrainerAvatar AvatarPrefab => avatarPrefab;
    public static bool UsesMobileControls
    {
        get
        {
#if UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
            return true;
#else
            return Application.isMobilePlatform;
#endif
        }
    }

    private void Start()
    {
        if (mobileControls != null) mobileControls.SetActive(UsesMobileControls);
        if (lobbyEventSystem != null && EventSystem.current != null && EventSystem.current != lobbyEventSystem)
            lobbyEventSystem.gameObject.SetActive(false);
        if (LobbyController.Instance == null)
        {
            Debug.LogWarning("Start from BootScene to connect to the open world lobby.");
            return;
        }
        AudioManager.StopAllMusic();
        AudioManager.PlayMusic(DefaultAudioMusic.MainLobbyCityTheme, true);
    }

    public void OpenSettings() => SettingsManager.Instance?.OpenSettings();
    public void OpenMatchMenu() => LobbyController.Instance?.OpenMatchLobbyMenu();

    private void Update()
    {
        if (status != null && LobbyController.Instance != null)
            status.text = LobbyController.Instance.OpenWorldLobby.Status;
    }

    public Vector3 SpawnPosition(ulong clientId)
    {
        var slot = (int)(clientId % 16);
        return spawnCenter + new Vector3((slot % 4 - 1.5f) * 1.2f, 0f, (slot / 4 - 1.5f) * 1.2f);
    }

}
