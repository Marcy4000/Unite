using JSAM;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BootSceneController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_InputField playerNameInputField;

    private void Start()
    {
        startButton.onClick.AddListener(OnStartButtonClicked);

        AudioManager.PlayMusic(DefaultAudioMusic.MainTheme);
    }

    private void OnStartButtonClicked()
    {
        if (string.IsNullOrWhiteSpace(playerNameInputField.text))
        {
            return;
        }

        AudioManager.StopMusic(DefaultAudioMusic.MainTheme);

        LobbyController.Instance.StartGame(playerNameInputField.text);
    }
}
