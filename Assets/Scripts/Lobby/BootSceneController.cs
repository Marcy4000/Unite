using JSAM;
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class BootSceneController : MonoBehaviour
{
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

    [SerializeField] private string preloadLabel = "preload";

    private AsyncOperationHandle downloadHandle;

    private void Start()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        NetworkManager.Singleton.LogLevel = LogLevel.Developer;
#else
        NetworkManager.Singleton.LogLevel = LogLevel.Error;
#endif
        startButton.onClick.AddListener(OnStartButtonClicked);
        versionText.text = $"v.{Application.version}";

        StartCoroutine(PlayAudioAfterLoading());

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD && UNITY_WEBGL
        StartCoroutine(DownloadAssets());
#else
        Addressables.InitializeAsync();
        hintIcon.SetActive(false);
        downloadWindow.Hide();
#endif
    }

    private IEnumerator PlayAudioAfterLoading()
    {
        yield return new WaitUntil(() => audioLibrary.IsLoaded());

        AudioManager.PlayMusic(DefaultAudioMusic.MainTheme, true);
    }

    private IEnumerator DownloadAssets()
    {
        startButton.interactable = false;
        clearCacheButton.interactable = false;
        downloadWindow.Hide();

        hintIcon.SetActive(true);

        yield return Addressables.InitializeAsync();

        downloadHandle = Addressables.DownloadDependenciesAsync(preloadLabel, false);
        float progress = 0;

        while (downloadHandle.Status == AsyncOperationStatus.None)
        {
            float percentageComplete = downloadHandle.GetDownloadStatus().Percent;
            if (percentageComplete > progress * 1.1) // Report at most every 10% or so
            {
                progress = percentageComplete; // More accurate %
                downloadAnnouncement.message = $"Downloading map assets... {string.Format("{0:0.00}", progress*100)}%\nPlease wait until the process is complete before playing\n" +
                $"Currently downloading: {downloadHandle.GetDownloadStatus().DownloadedBytes / (1024 * 1024)} MB / {downloadHandle.GetDownloadStatus().TotalBytes / (1024 * 1024)} MB";
                downloadWindow.SetAnnouncement(downloadAnnouncement);
            }
            yield return null;
        }

        clearCacheButton.interactable = true;

        if (downloadHandle.Status != AsyncOperationStatus.Succeeded)
        {
            downloadAnnouncement.message = $"Download failed!\nPlease try clearing the cache and restarting the game\nError: {downloadHandle.OperationException.Message}";
            downloadWindow.SetAnnouncement(downloadAnnouncement);
            yield break;
        }

        startButton.interactable = true;
        downloadAnnouncement.message = "Download complete!";
        downloadWindow.SetAnnouncement(downloadAnnouncement);
        Addressables.Release(downloadHandle); //Release the operation handle

        hintIcon.SetActive(false);
    }

    public void ClearCache()
    {
        Addressables.ClearDependencyCacheAsync(preloadLabel);
        downloadWindow.SetAnnouncement(new Announcement
        {
            title = "Download Manager",
            message = "Cache cleared!\nPlease restart the game to download the assets again"
        });
        startButton.interactable = false;
    }

    private void OnStartButtonClicked()
    {
        if (string.IsNullOrWhiteSpace(playerNameInputField.text))
        {
            return;
        }

        AudioManager.StopMusic(DefaultAudioMusic.MainTheme);
        AudioManager.PlaySound(DefaultAudioSounds.Home_ui_start);

        LobbyController.Instance.StartGame(playerNameInputField.text);
    }
}
