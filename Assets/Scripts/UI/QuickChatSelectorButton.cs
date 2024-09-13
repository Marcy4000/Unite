using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuickChatSelectorButton : MonoBehaviour
{
    [SerializeField] private Button button;

    private string message;

    public string Message => message;

    public System.Action<string> OnButtonClicked;

    public void SetMessage(string message)
    {
        this.message = message;
        button.GetComponentInChildren<TMP_Text>().text = message;

        button.onClick.AddListener(() =>
        {
            OnButtonClicked?.Invoke(message);
        });
    }

    public void Select()
    {
        button.Select();
    }

    public void Deselect()
    {
        button.OnDeselect(null);
    }
}
