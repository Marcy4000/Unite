using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private GameObject normalBG, bigBG;
    [SerializeField] private TMP_Text scoreText;

    private void Start()
    {
        normalBG.SetActive(false);
        bigBG.SetActive(false);
        scoreText.gameObject.SetActive(false);
    }

    public void ShowScore(int amount)
    {
        scoreText.gameObject.SetActive(true);
        scoreText.text = amount.ToString();
        if (amount >= 50)
        {
            bigBG.SetActive(true);
        }
        else
        {
            normalBG.SetActive(true);
        }
        
        StartCoroutine(HideScore());
    }

    private IEnumerator HideScore()
    {
        yield return new WaitForSeconds(2f);
        normalBG.SetActive(false);
        bigBG.SetActive(false);
        scoreText.gameObject.SetActive(false);
    }
}
