using JSAM;
using Unite.Updates;
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class BootSceneController : MonoBehaviour
{
    private const string PlayerNamePreferenceKey = "PlayerName";
    [SerializeField] private Button startButton;
    [SerializeField] private Button clearCacheButton;
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private TMP_Text versionText;
    [SerializeField] private MessageBox downloadWindow;
    [SerializeField] private GameObject hintIcon;
    [SerializeField] private AudioLibrary audioLibrary;

    private Announcement downloadAnnouncement = new Announcement
    {
        title = "Download Manager",
        message = "Please wait while we download the game assets."
    };

    private bool updating;
    private bool ready;

    private void Start()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.LogLevel = LogLevel.Developer;
#else
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.LogLevel = LogLevel.Error;
#endif
        startButton.interactable = false;
        startButton.onClick.AddListener(OnStartButtonClicked);
        RestorePlayerName();
        StartCoroutine(PlayAudioAfterLoading());
        StartCoroutine(DownloadAssets());
    }

    private IEnumerator PlayAudioAfterLoading()
    {
        yield return new WaitUntil(() => audioLibrary.IsLoaded());
        AudioManager.PlayMusic(DefaultAudioMusic.MainTheme, true);
    }

    private IEnumerator DownloadAssets()
    {
        if (updating || ready) yield break;
        updating = true;
        startButton.interactable = false;
        clearCacheButton.interactable = false;
        hintIcon.SetActive(true);
        downloadWindow.Show();
        versionText.text = "v." + Application.version + " — checking updates";
        var downloader = new UpdateDownloader(message =>
        {
            downloadAnnouncement.message = message;
            downloadWindow.SetAnnouncement(downloadAnnouncement);
        });
        yield return downloader.Run(UpdateDeliverySettings.Load());
        updating = false;
        ready = downloader.Succeeded;
        startButton.interactable = ready;
        clearCacheButton.interactable = !ready;
        hintIcon.SetActive(!ready);
        versionText.text = "v." + GameRelease.Display;
        if (ready) downloadWindow.Hide();
        else
        {
            downloadAnnouncement.message = downloader.Error + "\nUse Retry update to try again. Previously downloaded content is kept.";
            downloadWindow.SetAnnouncement(downloadAnnouncement);
        }
    }

    // Retain the serialized legacy button binding. It now retries without deleting valid cached maps.
    public void ClearCache()
    {
        if (!updating && !ready) StartCoroutine(DownloadAssets());
    }

    private void OnStartButtonClicked()
    {
        if (!ready || !GameRelease.Ready || !SavePlayerName(playerNameInputField.text))
        {
            return;
        }

        AudioManager.StopMusic(DefaultAudioMusic.MainTheme);
        AudioManager.PlaySound(DefaultAudioSounds.Home_ui_start);

        LobbyController.Instance.StartGame(playerNameInputField.text);
    }

    private void RestorePlayerName()
    {
        var savedName = PlayerPrefs.GetString(PlayerNamePreferenceKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(savedName)) playerNameInputField.text = savedName;
    }

    private static bool SavePlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) return false;
        PlayerPrefs.SetString(PlayerNamePreferenceKey, playerName);
        PlayerPrefs.Save();
        return true;
    }
}
