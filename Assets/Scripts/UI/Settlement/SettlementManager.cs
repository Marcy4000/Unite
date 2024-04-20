using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class SettlementManager : MonoBehaviour
{
    [SerializeField] private ResultBarsUI resultBarsUI;
    [SerializeField] private GameInfoUI gameInfoUI;

    [SerializeField] private TMP_Text blueScoreText;
    [SerializeField] private TMP_Text orangeScoreText;

    [SerializeField] private Button returnLobbyButton;

    private int blueScoreValue;
    private int orangeScoreValue;

    private void Start()
    {
        gameInfoUI.Initialize();
        blueScoreText.gameObject.SetActive(false);
        orangeScoreText.gameObject.SetActive(false);

        returnLobbyButton.onClick.AddListener(() =>
        {
            LobbyController.Instance.ReturnToLobby();
        });

        LoadingScreen.Instance.HideGenericLoadingScreen();

        ShowScore();
    }

    public void ShowScore()
    {
        resultBarsUI.gameObject.SetActive(true);
        gameInfoUI.gameObject.SetActive(false);


        blueScoreValue = int.Parse(LobbyController.Instance.Lobby.Data["BlueTeamScore"].Value);
        orangeScoreValue = int.Parse(LobbyController.Instance.Lobby.Data["OrangeTeamScore"].Value);

        int maxScore = blueScoreValue;
        if (orangeScoreValue > maxScore)
        {
            maxScore = orangeScoreValue;
        }

        resultBarsUI.InitializeUI(maxScore);

        StartCoroutine(ShowScoreRoutine());
    }

    private IEnumerator ShowScoreRoutine()
    {
        bool finished = false;
        int blueScoreValue = 0;
        int orangeScoreValue = 0;



        while (!finished)
        {
            if (blueScoreValue < this.blueScoreValue)
            {
                blueScoreValue = Mathf.Min(this.blueScoreValue, blueScoreValue + 5);
            }

            if (orangeScoreValue < this.orangeScoreValue)
            {
                orangeScoreValue = Mathf.Min(this.orangeScoreValue, orangeScoreValue + 5);
            }

            resultBarsUI.SetBars(blueScoreValue, orangeScoreValue);

            if (blueScoreValue == this.blueScoreValue && orangeScoreValue == this.orangeScoreValue)
            {
                finished = true;
                yield return new WaitForSeconds(1f);
                OnShowScoreEnded();
            }

            yield return new WaitForSeconds(0.05f);
        }
    }


    private void OnShowScoreEnded()
    {
        resultBarsUI.gameObject.SetActive(false);
        gameInfoUI.gameObject.SetActive(true);

        blueScoreText.gameObject.SetActive(true);
        orangeScoreText.gameObject.SetActive(true);

        blueScoreText.text = blueScoreValue.ToString();
        orangeScoreText.text = orangeScoreValue.ToString();
    }
}
