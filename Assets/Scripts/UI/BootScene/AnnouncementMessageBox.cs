using TMPro;
using UnityEngine;

public class AnnouncementMessageBox : MonoBehaviour
{
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text content;

    public void SetAnnouncement(Announcement announcement)
    {
        title.text = announcement.title;
        content.text = announcement.message;
    }
}
