using DG.Tweening;
using JSAM;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DefaultStartAnimationUI : BaseStartAnimationUI
{
    [SerializeField] private GameObject readyBar;
    [SerializeField] private Image background;
    [SerializeField] private RectTransform readyText, goBar;

    protected override IEnumerator DoStartAnimation()
    {
        StartCoroutine(HandleSounds());
        background.gameObject.SetActive(true);
        background.color = new Color(background.color.r, background.color.g, background.color.b, 0.75f);
        readyBar.SetActive(true);
        goBar.gameObject.SetActive(false);

        readyText.anchoredPosition = new Vector2(282f, -47.144f);

        yield return readyText.DOAnchorPosX(0, 2f).WaitForCompletion();

        yield return new WaitForSeconds(0.5f);

        readyBar.SetActive(false);
        goBar.gameObject.SetActive(true);

        goBar.localScale = new Vector3(1.6f, 1.6f, 1);

        goBar.DOScale(Vector3.one, 0.25f).onComplete += () =>
        {
            goBar.DOScale(Vector3.one * 2, 0.583f);
        };

        yield return new WaitForSeconds(0.4f);

        background.DOFade(0, 0.5f);

        yield return new WaitForSeconds(0.5f);

        background.gameObject.SetActive(false);
        goBar.gameObject.SetActive(false);
        readyBar.SetActive(false);
    }

    private IEnumerator HandleSounds()
    {
        AudioManager.PlaySound(DefaultAudioSounds.Game_ui_Rookie_Scoreboard_1);

        yield return new WaitForSeconds(0.2f);

        MapInfo currentMap = GameManager.Instance.CurrentMap;
        AudioManager.PlayMusic(currentMap.normalMusic, true);

        yield return new WaitForSeconds(0.4f);

        AudioManager.PlaySound(DefaultAudioSounds.AnnouncerReady);

        yield return new WaitForSeconds(1.6f);

        AudioManager.PlaySound(DefaultAudioSounds.Game_ui_Rookie_Scoreboard_Go);

        yield return new WaitForSeconds(0.9f);
    }

    protected override void ResetAnimation()
    {
        background.DOKill();
        readyText.DOKill();
        goBar.DOKill();

        background.gameObject.SetActive(false);
        goBar.gameObject.SetActive(false);
        readyBar.SetActive(false);
    }
}
