using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManagerUI : MonoBehaviour
{
    public static NetworkManagerUI instance;

    [SerializeField] private Button serverButton;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button startGameButton;

    [SerializeField] private GameObject scoreBoard;
    [SerializeField] private Image blueBar, orangeBar;
    [SerializeField] private TMP_Text blueScore, orangeScore;
    [SerializeField] private Button returnLobbyButton;

    private void Awake()
    {
        instance = this;

        serverButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartServer();
        });

        hostButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartHost();
        });

        clientButton.onClick.AddListener(() =>
        {
            NetworkManager.Singleton.StartClient();
        });

        returnLobbyButton.onClick.AddListener(() =>
        {
            LobbyController.instance.ReturnToLobby();
        });
    }

    public void DebugShowScore()
    {
        scoreBoard.SetActive(true);
        blueBar.fillAmount = 0f;
        orangeBar.fillAmount = 0f;
        blueScore.text = "0";
        orangeScore.text = "0";

        returnLobbyButton.gameObject.SetActive(NetworkManager.Singleton.IsHost);

        StartCoroutine(ShowScoreRoutine());
    }

    private IEnumerator ShowScoreRoutine()
    {
        bool finished = false;
        int blueScoreValue = 0;
        int orangeScoreValue = 0;

        int maxScore = GameManager.Instance.BlueTeamScore;
        if (GameManager.Instance.OrangeTeamScore > maxScore)
        {
            maxScore = GameManager.Instance.OrangeTeamScore;
        }

        while (!finished)
        {
            if (blueScoreValue < GameManager.Instance.BlueTeamScore)
            {
                blueScoreValue = Mathf.Min(GameManager.Instance.BlueTeamScore, blueScoreValue + 5);
            }

            if (orangeScoreValue < GameManager.Instance.OrangeTeamScore)
            {
                orangeScoreValue = Mathf.Min(GameManager.Instance.OrangeTeamScore, orangeScoreValue + 5);
            }

            blueBar.fillAmount = (float)blueScoreValue / maxScore;
            orangeBar.fillAmount = (float)orangeScoreValue / maxScore;
            blueScore.text = blueScoreValue.ToString();
            orangeScore.text = orangeScoreValue.ToString();

            if (blueScoreValue == GameManager.Instance.BlueTeamScore && orangeScoreValue == GameManager.Instance.OrangeTeamScore)
            {
                finished = true;
            }

            yield return new WaitForSeconds(0.1f);
        }
    }
}
