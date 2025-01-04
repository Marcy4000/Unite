using TMPro;
using UnityEngine;

public class AnnouncementEditor : MonoBehaviour
{
    [SerializeField] private TMP_InputField titleField, contentField;
    [SerializeField] private TMP_Text previewText;

    public void SaveAnnouncement()
    {
        Announcement announcement = new Announcement
        {
            title = titleField.text,
            message = contentField.text,
            date = System.DateTime.Now.ToString("dd/MM/yyyy"),
            appVersion = Application.version
        };

        string json = JsonUtility.ToJson(announcement, true);
        Debug.Log(json);

        SaveJsonToFile(json, "announcement.json");
    }

    public void LoadAnnouncement()
    {
        string path = Application.dataPath + "/announcement.json";
        string json = System.IO.File.ReadAllText(path);
        Debug.Log(json);

        Announcement announcement = JsonUtility.FromJson<Announcement>(json);
        titleField.text = announcement.title;
        contentField.text = announcement.message;
    }

    void SaveJsonToFile(string json, string fileName)
    {
        string path = Application.dataPath + "/" + fileName;
        System.IO.File.WriteAllText(path, json);
        Debug.Log("JSON saved to: " + path);
    }

    public void EnableRichText(bool enable)
    {
        previewText.richText = enable;
    }
}
