using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class AnnouncementManager : MonoBehaviour
{
    [SerializeField] private AnnouncementMessageBox messageBox;
    private string jsonFileUrl = "https://drive.google.com/uc?export=download&id=1lzgromQKs8eySkDQdo3f1vCs4PKENZ1D";

    private Announcement loadingAnnouncement = new Announcement
    {
        title = "Loading...",
        message = "Downloading latest announcement, please wait...",
        date = ""
    };

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
        StartCoroutine(DownloadAndDisplayAnnouncement());
    }

    private IEnumerator DownloadAndDisplayAnnouncement()
    {
        messageBox.SetAnnouncement(loadingAnnouncement);
        UnityWebRequest request = UnityWebRequest.Get(jsonFileUrl);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError("Error downloading JSON file: " + request.error);
            messageBox.SetAnnouncement(new Announcement
            {
                title = "Error",
                message = "Failed to download announcement. Please try again later.",
                date = ""
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
            }
            catch (System.Exception e) {
                Debug.LogError("Error parsing JSON content: " + e.Message);
                messageBox.SetAnnouncement(new Announcement
                {
                    title = "Error",
                    message = "Failed to parse announcement. If you see this error then i messed up\nPlease try again later.",
                    date = ""
                });
            }
        }
    }
}
