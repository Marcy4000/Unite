using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class AnnouncementManager : MonoBehaviour
{
    [SerializeField] private MessageBox messageBox;
    [SerializeField] private MessageBox wrongVersionPopup;

    private string jsonFileUrl = "https://drive.google.com/uc?export=download&id=1lzgromQKs8eySkDQdo3f1vCs4PKENZ1D"; // This doesn't work on WebGL

    private Announcement loadingAnnouncement = new Announcement
    {
        title = "Loading...",
        message = "Downloading latest announcement, please wait...",
        date = "",
    };

    private Announcement webGlAnnouncement = new Announcement
    {
        title = "WebGL Notice",
        message = "WebGL build currently does not support downloading announcements.",
        date = "",
    };

    private void Awake()
    {
        loadingAnnouncement.appVersion = Application.version;
        webGlAnnouncement.appVersion = Application.version;
    }

    public void ShowAnnouncement()
    {
        gameObject.SetActive(true);
    }

    public void HideAnnouncement()
    {
        gameObject.SetActive(false);
    }

    public void Start()
    {
        wrongVersionPopup.Hide();
        StartCoroutine(DownloadAndDisplayAnnouncement());
    }

    private IEnumerator DownloadAndDisplayAnnouncement()
    {
        messageBox.SetAnnouncement(loadingAnnouncement);

        if (Application.platform == RuntimePlatform.WebGLPlayer)
        {
            messageBox.SetAnnouncement(webGlAnnouncement);
            yield break;
        }

        UnityWebRequest request = UnityWebRequest.Get(jsonFileUrl);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error downloading JSON file: " + request.error);
            messageBox.SetAnnouncement(new Announcement
            {
                title = "Error",
                message = "Failed to download announcement. Please try again later.",
                date = "",
                appVersion = Application.version
            });
        }
        else
        {
            // Save the downloaded JSON content
            string jsonContent = request.downloadHandler.text;

            // Parse the JSON content
            try
            {
                Announcement notification = JsonUtility.FromJson<Announcement>(jsonContent);
                messageBox.SetAnnouncement(notification);

                if (!notification.appVersion.Equals(Application.version))
                {
                    string message = $"It appears you are using an outdated version of the game." +
                        $"\nYou're running version {Application.version}, but {notification.appVersion} is the latest version.\n" +
                        $"You won't be able to play with players using a differnt version, please download the latest version of the game from either itch.io or gamejolt";
                    wrongVersionPopup.Show();
                    wrongVersionPopup.SetMessage("Attention!", message);
                }
            }
            catch (System.Exception e) {
                Debug.LogError("Error parsing JSON content: " + e.Message);
                messageBox.SetAnnouncement(new Announcement
                {
                    title = "Error",
                    message = "Failed to parse announcement. If you see this error then i messed up\nPlease try again later.",
                    date = "",
                    appVersion = Application.version
                });
            }
        }
    }
}
