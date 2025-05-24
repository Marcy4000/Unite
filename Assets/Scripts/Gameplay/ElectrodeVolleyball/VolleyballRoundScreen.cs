using DG.Tweening;
using JSAM;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VolleyballRoundScreen : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private RectTransform readyText, topBar, bottomBar, topBarFinalStretch, bottomBarFinalStretch;

    private TMP_Text readyTextTMP;

    void Start()
    {
        ResetAnimation();
        readyTextTMP = readyText.GetComponent<TMP_Text>();
    }

    // Permetti di scegliere la versione final stretch
    public void StartAnimation(bool useFinalStretch)
    {
        StartCoroutine(DoStartAnimation(useFinalStretch));
    }

    private void ResetAnimation()
    {
        background.DOKill();
        readyText.DOKill();
        topBar.DOKill();
        bottomBar.DOKill();
        topBarFinalStretch.DOKill();
        bottomBarFinalStretch.DOKill();

        background.gameObject.SetActive(false);
        readyText.gameObject.SetActive(false);
        topBar.gameObject.SetActive(false);
        bottomBar.gameObject.SetActive(false);
        topBarFinalStretch.gameObject.SetActive(false);
        bottomBarFinalStretch.gameObject.SetActive(false);
    }

    private IEnumerator DoStartAnimation(bool useFinalStretch)
    {
        // Imposta il testo pronto
        readyTextTMP.text = useFinalStretch ? "Match Point!" : "Round Starts...";

        AudioManager.PlaySound(DefaultAudioSounds.Game_ui_Rookie_Scoreboard_1);

        background.gameObject.SetActive(true);
        background.color = new Color(background.color.r, background.color.g, background.color.b, 0.75f);

        // Scegli le barre da animare
        RectTransform top = useFinalStretch ? topBarFinalStretch : topBar;
        RectTransform bottom = useFinalStretch ? bottomBarFinalStretch : bottomBar;

        // Attiva solo le barre scelte
        top.gameObject.SetActive(true);
        bottom.gameObject.SetActive(true);
        (useFinalStretch ? topBar : topBarFinalStretch).gameObject.SetActive(false);
        (useFinalStretch ? bottomBar : bottomBarFinalStretch).gameObject.SetActive(false);

        // Posizioni di partenza e arrivo
        float screenWidth = ((RectTransform)top.parent).rect.width;
        Vector2 topStart = new Vector2(-screenWidth, top.anchoredPosition.y);
        Vector2 topCenter = new Vector2(0, top.anchoredPosition.y);
        Vector2 topEnd = new Vector2(screenWidth, top.anchoredPosition.y);

        Vector2 bottomStart = new Vector2(screenWidth, bottom.anchoredPosition.y);
        Vector2 bottomCenter = new Vector2(0, bottom.anchoredPosition.y);
        Vector2 bottomEnd = new Vector2(-screenWidth, bottom.anchoredPosition.y);

        // Imposta posizioni iniziali
        top.anchoredPosition = topStart;
        bottom.anchoredPosition = bottomStart;

        // Prepara readyText per fade/zoom in
        readyText.gameObject.SetActive(true);
        readyText.localScale = Vector3.one * 0.7f;
        var readyTextCanvasGroup = readyText.GetComponent<CanvasGroup>();
        if (readyTextCanvasGroup == null)
            readyTextCanvasGroup = readyText.gameObject.AddComponent<CanvasGroup>();
        readyTextCanvasGroup.alpha = 0f;

        // Fai entrare le barre (veloce)
        float inDuration = 0.5f;
        Tween topIn = top.DOAnchorPos(topCenter, inDuration).SetEase(Ease.OutCubic);
        Tween bottomIn = bottom.DOAnchorPos(bottomCenter, inDuration).SetEase(Ease.OutCubic);

        // Pop-in del testo a metà ingresso barre
        float readyInDelay = inDuration * 0.5f;
        float readyInDuration = 0.25f;
        yield return new WaitForSeconds(readyInDelay);
        readyText.DOScale(1f, readyInDuration).SetEase(Ease.OutBack);
        readyTextCanvasGroup.DOFade(1f, readyInDuration);

        // Attendi che le barre finiscano di entrare (se serve)
        float remainingIn = inDuration - readyInDelay;
        if (remainingIn > readyInDuration)
            yield return new WaitForSeconds(remainingIn - readyInDuration);
        else
            yield return new WaitForSeconds(remainingIn);

        yield return topIn.WaitForCompletion();
        yield return bottomIn.WaitForCompletion();

        // Movimento lento verso l'uscita (quasi statico)
        float slowMoveDuration = 1.0f;
        Vector2 topSlow = Vector2.Lerp(topCenter, topEnd, 0.1f);
        Vector2 bottomSlow = Vector2.Lerp(bottomCenter, bottomEnd, 0.1f);

        Tween topSlowMove = top.DOAnchorPos(topSlow, slowMoveDuration).SetEase(Ease.Linear);
        Tween bottomSlowMove = bottom.DOAnchorPos(bottomSlow, slowMoveDuration).SetEase(Ease.Linear);

        yield return new WaitForSeconds(slowMoveDuration);

        // Uscita veloce
        float outDuration = 0.35f;
        Tween topOut = top.DOAnchorPos(topEnd, outDuration).SetEase(Ease.InCubic);
        Tween bottomOut = bottom.DOAnchorPos(bottomEnd, outDuration).SetEase(Ease.InCubic);

        // Pop-out del testo leggermente dopo l'inizio uscita barre
        float readyOutDelay = outDuration * 0.3f;
        float readyOutDuration = 0.2f;
        yield return new WaitForSeconds(readyOutDelay);
        readyText.DOScale(1.2f, readyOutDuration).SetEase(Ease.InBack);
        readyTextCanvasGroup.DOFade(0f, readyOutDuration);

        // Attendi che le barre finiscano di uscire
        yield return topOut.WaitForCompletion();
        yield return bottomOut.WaitForCompletion();

        // Fade out background
        background.DOFade(0, 0.5f);
        yield return new WaitForSeconds(0.5f);

        background.gameObject.SetActive(false);
        top.gameObject.SetActive(false);
        bottom.gameObject.SetActive(false);
        readyText.gameObject.SetActive(false);
    }
}
