using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private GameObject normalBG, bigBG, holder;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Image portrait;

    private void Start()
    {
        normalBG.SetActive(false);
        bigBG.SetActive(false);
        holder.SetActive(false);
    }

    public void ShowScore(int amount, Sprite portrait)
    {
        holder.SetActive(true);
        scoreText.text = amount.ToString();
        this.portrait.sprite = portrait;
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
        holder.SetActive(false);
    }
}
