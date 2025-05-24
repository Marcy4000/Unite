using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class VolleyballPointsCounter : MonoBehaviour
{
    [SerializeField] private TMP_Text blueScoreText;
    [SerializeField] private TMP_Text orangeScoreText;

    [SerializeField] private GameObject[] blueDots;
    [SerializeField] private GameObject[] orangeDots;

    private void Update()
    {
        int orangeTeamScore = GameManager.Instance.OrangeTeamScore;
        int blueTeamScore = GameManager.Instance.BlueTeamScore;

        blueScoreText.text = blueTeamScore.ToString();
        orangeScoreText.text = orangeTeamScore.ToString();

        UpdateScoreDots(blueDots, blueTeamScore);
        UpdateScoreDots(orangeDots, orangeTeamScore);
    }

    private void UpdateScoreDots(GameObject[] dots, int score)
    {
        for (int i = 0; i < dots.Length; i++)
        {
            dots[i].SetActive(i < score);
        }
    }
}
