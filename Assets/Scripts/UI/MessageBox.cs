using TMPro;
using UnityEngine;

public class MessageBox : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;

    public void SetMessage(string title, string message)
    {
        titleText.text = title;
        messageText.text = message;
    }

    public void SetAnnouncement(Announcement announcement)
    {
        titleText.text = announcement.title;
        messageText.text = announcement.message;
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
