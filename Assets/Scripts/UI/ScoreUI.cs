using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private GameObject normalBG, bigBG, holder;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Image portrait;
    [SerializeField] private Vector2 entranceOffset = new Vector2(-60f, -12f);

    private readonly Queue<ScoreUIInfo> scoreQueue = new Queue<ScoreUIInfo>();
    private Sequence scoreSequence;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 restingPosition;
    private Vector3 restingScale;
    private Vector3 textScale;
    private bool isShowingScore;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        restingPosition = rectTransform.anchoredPosition;
        restingScale = rectTransform.localScale;
        textScale = scoreText.transform.localScale;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        HideScore();
    }

    public void EnqueueScore(ScoreUIInfo info)
    {
        scoreQueue.Enqueue(info);
    }

    private void ShowScore(ScoreUIInfo info)
    {
        isShowingScore = true;
        bool isBigScore = info.amount >= 50;
        normalBG.SetActive(!isBigScore);
        bigBG.SetActive(isBigScore);
        holder.SetActive(true);
        scoreText.text = info.amount.ToString();
        portrait.sprite = info.portrait;

        rectTransform.anchoredPosition = restingPosition + entranceOffset;
        rectTransform.localScale = restingScale * 0.88f;
        scoreText.transform.localScale = textScale * 0.65f;
        canvasGroup.alpha = 0f;

        // One sequence owns the entire notification, including its readable hold.
        scoreSequence = DOTween.Sequence();
        scoreSequence.Append(rectTransform.DOAnchorPos(restingPosition, 0.32f).SetEase(Ease.OutCubic));
        scoreSequence.Join(rectTransform.DOScale(restingScale, 0.32f).SetEase(Ease.OutBack));
        scoreSequence.Join(canvasGroup.DOFade(1f, 0.18f));
        scoreSequence.Insert(0.12f, scoreText.transform
            .DOScale(textScale * (isBigScore ? 1.3f : 1.12f), 0.2f).SetEase(Ease.OutBack));
        scoreSequence.Append(scoreText.transform.DOScale(textScale, 0.16f).SetEase(Ease.OutQuad));
        scoreSequence.AppendInterval(1.78f);
        scoreSequence.Append(canvasGroup.DOFade(0f, 0.24f).SetEase(Ease.InQuad));
        scoreSequence.Join(rectTransform.DOAnchorPos(restingPosition + Vector2.up * 18f, 0.24f)
            .SetEase(Ease.InQuad));
        scoreSequence.Join(rectTransform.DOScale(restingScale * 0.96f, 0.24f).SetEase(Ease.InQuad));
        scoreSequence.OnComplete(() =>
        {
            scoreSequence = null;
            HideScore();
        });
    }

    private void Update()
    {
        if (!isShowingScore && scoreQueue.Count > 0)
        {
            ShowScore(scoreQueue.Dequeue());
        }
    }

    private void OnDisable()
    {
        scoreSequence?.Kill();
        scoreSequence = null;
        HideScore();
    }

    private void HideScore()
    {
        normalBG.SetActive(false);
        bigBG.SetActive(false);
        holder.SetActive(false);
        rectTransform.anchoredPosition = restingPosition;
        rectTransform.localScale = restingScale;
        scoreText.transform.localScale = textScale;
        canvasGroup.alpha = 0f;
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