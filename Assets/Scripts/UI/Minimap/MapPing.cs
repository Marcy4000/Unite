using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class MapPing : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Image circle1, circle2;

    private MinimapIcon minimapIcon;

    public MinimapIcon MinimapIcon => minimapIcon;

    public void DoAnimation()
    {
        minimapIcon = GetComponent<MinimapIcon>();

        // Reset icon and circles
        icon.transform.localScale = Vector3.one * 1.5f;
        icon.color = new Color(icon.color.r, icon.color.g, icon.color.b, 0);

        circle1.transform.localScale = Vector3.zero;
        circle1.color = new Color(circle1.color.r, circle1.color.g, circle1.color.b, 1);

        circle2.transform.localScale = Vector3.zero;
        circle2.color = new Color(circle2.color.r, circle2.color.g, circle2.color.b, 1);

        // Icon animation: fade in and shrink to normal size
        icon.DOFade(1, 0.3f);
        icon.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);

        // Circle1 animation sequence
        Sequence circle1Sequence = DOTween.Sequence();
        circle1Sequence.Append(circle1.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutCirc)) // Slow growth
            .Append(circle1.DOFade(0, 0.2f)) // Quick fade out
            .OnComplete(() => circle1.transform.localScale = Vector3.zero); // Reset scale for next replay

        // Circle2 animation sequence
        Sequence circle2Sequence = DOTween.Sequence();
        circle2Sequence.AppendInterval(0.3f) // Start when Circle1 starts fading out
            .Append(circle2.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutCirc)) // Slow growth
            .Append(circle2.DOFade(0, 0.2f)) // Quick fade out
            .OnComplete(() => circle2.transform.localScale = Vector3.zero); // Reset scale for next replay

        // Combined sequence for replay and final despawn
        Sequence pingSequence = DOTween.Sequence()
            .Append(circle1Sequence)
            .Join(circle2Sequence)
            .AppendInterval(0.15f)
            .SetLoops(2)
            .OnComplete(() => Destroy(gameObject));

        pingSequence.Play();
    }
}