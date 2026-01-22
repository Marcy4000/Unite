using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using JSAM;
using TMPro;
using DG.Tweening;

public class RacingStartAnimationUI : BaseStartAnimationUI
{
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private Image background, psyduckOverlay;

    protected override IEnumerator DoStartAnimation()
    {
        background.gameObject.SetActive(true);
        countdownText.gameObject.SetActive(true);
        psyduckOverlay.gameObject.SetActive(true);

        background.color = Color.clear;
        psyduckOverlay.color = new Color(1, 1, 1, 0);
        countdownText.transform.localScale = Vector3.zero;

        Sequence seq = DOTween.Sequence();

        seq.Append(background.DOFade(0.7f, 0.5f));
        seq.Append(psyduckOverlay.DOFade(1f, 0.5f));
        seq.AppendInterval(0.5f);

        seq.AppendCallback(() =>
        {
            AudioManager.PlaySound(DefaultAudioSounds.snd_wanfa_KeDaYa6);
        });

        string[] countdowns = { "3", "2", "1" };

        foreach (string val in countdowns)
        {
            seq.AppendCallback(() =>
            {
                countdownText.text = val;
                countdownText.fontSize = 200;
            });

            seq.Append(countdownText.transform.DOScale(1, 0.2f).SetEase(Ease.OutBack));
            seq.AppendInterval(0.6f);
            seq.Append(countdownText.transform.DOScale(0, 0.2f));
        }

        seq.AppendCallback(() =>
        {
            countdownText.text = "GO!";
            countdownText.fontSize = 260;
        });
        seq.Append(countdownText.transform.DOScale(1, 0.2f).SetEase(Ease.OutBack));
        seq.AppendInterval(1f);

        seq.Append(countdownText.transform.DOScale(0, 0.3f));
        seq.Append(background.DOFade(0, 0.5f));
        seq.Append(psyduckOverlay.DOFade(0, 0.5f));

        yield return seq.WaitForCompletion();

        MapInfo currentMap = CharactersList.Instance.GetCurrentLobbyMap();
        AudioManager.PlayMusic(currentMap.normalMusic, true);

        countdownText.gameObject.SetActive(false);
        background.gameObject.SetActive(false);
        psyduckOverlay.gameObject.SetActive(false);
    }

    protected override void ResetAnimation()
    {
        countdownText.transform.DOKill();
        background.DOKill();

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }
        if (background != null)
        {
            background.gameObject.SetActive(false);
        }
    }
}
