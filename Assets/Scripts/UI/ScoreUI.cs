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

    private Queue<ScoreUIInfo> scoreQueue = new Queue<ScoreUIInfo>();

    private bool isShowingScore;

    private void Start()
    {
        normalBG.SetActive(false);
        bigBG.SetActive(false);
        holder.SetActive(false);
    }

    public void EnqueueScore(ScoreUIInfo info)
    {
        scoreQueue.Enqueue(info);
    }

    private void ShowScore(ScoreUIInfo info)
    {
        holder.SetActive(true);
        scoreText.text = info.amount.ToString();
        portrait.sprite = info.portrait;
        if (info.amount >= 50)
        {
            bigBG.SetActive(true);
        }
        else
        {
            normalBG.SetActive(true);
        }
        
        StartCoroutine(HideScore());
    }

    private void Update()
    {
        if (!isShowingScore)
        {
            if (scoreQueue.Count > 0)
            {
                ShowScore(scoreQueue.Dequeue());
            }
        }
    }

    private IEnumerator HideScore()
    {
        isShowingScore = true;
        yield return new WaitForSeconds(2.5f);
        normalBG.SetActive(false);
        bigBG.SetActive(false);
        holder.SetActive(false);
        isShowingScore = false;
    }
}

public class ScoreUIInfo
{
    public int amount;
    public Sprite portrait;

    public ScoreUIInfo(int amount, Sprite portrait)
    {
        this.amount = amount;
        this.portrait = portrait;
    }
}