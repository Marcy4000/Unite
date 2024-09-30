using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class DraftBanHolder : MonoBehaviour
{
    [SerializeField] private GameObject pokemonIconHolder, banIcon;
    [SerializeField] private Image pokemonIcon;

    private void Start()
    {
        pokemonIconHolder.SetActive(false);
        banIcon.SetActive(false);
    }

    public void SetBanIcon(CharacterInfo info)
    {
        if (info == null)
        {
            return;
        }

        pokemonIconHolder.SetActive(true);
        pokemonIcon.sprite = info.icon;

        pokemonIcon.color = new Color(1, 1, 1, 0.1f);
        pokemonIcon.DOColor(Color.white, 0.5f);

        pokemonIcon.transform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack).onComplete += () =>
        {
            banIcon.gameObject.SetActive(true);
            banIcon.transform.localScale = new Vector3(1.5f, 1.5f,1.5f);
            banIcon.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.InBack);
        };
    }
}
