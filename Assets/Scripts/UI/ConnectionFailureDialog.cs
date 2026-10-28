using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ConnectionFailureDialog : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button dismissButton;

    public TMP_Text TitleText => titleText;
    public TMP_Text MessageText => messageText;
    public Button DismissButton => dismissButton;
}
