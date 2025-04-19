using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class MapObjective : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Image circle1, circle2;

    private MinimapIcon minimapIcon;

    private WildPokemon wildPokemon;

    public MinimapIcon MinimapIcon => minimapIcon;

    public void Initialize(WildPokemon wildPokemon)
    {
        minimapIcon = GetComponent<MinimapIcon>();
        this.wildPokemon = wildPokemon;
        minimapIcon.SetTarget(wildPokemon.transform);

        wildPokemon.Pokemon.OnDeath += (info) => { Destroy(gameObject); };
    }

    public void HideIcon()
    {
        icon.gameObject.SetActive(false);
    }

    public void ShowIcon()
    {
        icon.gameObject.SetActive(true);
    }

    public void SetVisibility(bool isVisible)
    {
        icon.gameObject.SetActive(isVisible);
    }

    public void DoAnimation()
    {
        // Reset circles
        circle1.gameObject.SetActive(true);
        circle1.transform.localScale = Vector3.one;
        circle1.color = new Color(circle1.color.r, circle1.color.g, circle1.color.b, 0);

        circle2.gameObject.SetActive(true);
        circle2.transform.localScale = Vector3.one;
        circle2.color = new Color(circle2.color.r, circle2.color.g, circle2.color.b, 0);

        // Circle1 animation sequence
        Sequence circle1Sequence = DOTween.Sequence();
        circle1Sequence.AppendCallback(() => circle1.color = new Color(circle1.color.r, circle1.color.g, circle1.color.b, 1)) // Fade in circle1
            .Append(circle1.transform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InCirc)) // Slow shrink
            .OnComplete(() =>
            {
                circle1.transform.localScale = Vector3.one;
                circle1.color = new Color(circle1.color.r, circle1.color.g, circle1.color.b, 0); // Reset color for next replay
            }); // Reset scale for next replay

        // Circle2 animation sequence
        Sequence circle2Sequence = DOTween.Sequence();
        circle2Sequence.AppendInterval(0.3f) // Start when Circle1 is partially done
            .AppendCallback(() => circle2.color = new Color(circle2.color.r, circle2.color.g, circle2.color.b, 1)) // Fade in circle2
            .Append(circle2.transform.DOScale(Vector3.zero, 0.4f).SetEase(Ease.InCirc)) // Slow shrink
            .OnComplete(() =>
            {
                circle2.transform.localScale = Vector3.one;
                circle2.color = new Color(circle2.color.r, circle2.color.g, circle2.color.b, 0); // Reset color for next replay
            }); // Reset scale for next replay

        // Combined sequence for replay and final despawn
        Sequence pingSequence = DOTween.Sequence()
            .Append(circle1Sequence)
            .Join(circle2Sequence)
            .AppendInterval(0.15f)
            .SetLoops(2)
            .OnComplete(() =>
            {
                circle1.gameObject.SetActive(false);
                circle2.gameObject.SetActive(false);
            });

        pingSequence.Play();
    }
}
