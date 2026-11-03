using JSAM;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class OpenWorldLobbyView : MonoBehaviour
{
    [SerializeField] private OpenWorldTrainerAvatar avatarPrefab;
    [SerializeField] private Vector3 spawnCenter = new Vector3(0f, 0.3f, 0f);
    [SerializeField] private TMP_Text status;
    [SerializeField] private GameObject mobileControls;
    [SerializeField] private EventSystem lobbyEventSystem;
    public OpenWorldTrainerAvatar AvatarPrefab => avatarPrefab;

    private void Start()
    {
        if (mobileControls != null) mobileControls.SetActive(Application.isMobilePlatform);
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
