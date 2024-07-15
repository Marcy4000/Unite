using JSAM;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class BootSceneController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private TMP_Text versionText;

    private void Start()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.unityLogger.logEnabled = true;
        NetworkManager.Singleton.LogLevel = LogLevel.Developer;
#else
        Debug.unityLogger.logEnabled = false;
        NetworkManager.Singleton.LogLevel = LogLevel.Error;
#endif
        startButton.onClick.AddListener(OnStartButtonClicked);
        versionText.text = $"v.{Application.version}";

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
