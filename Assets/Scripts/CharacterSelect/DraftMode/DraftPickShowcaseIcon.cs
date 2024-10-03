using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DraftPickShowcaseIcon : MonoBehaviour
{
    [SerializeField] private Image pokemonPortrait;
    [SerializeField] private TMP_Text pokemonName;
    [SerializeField] private RectTransform bgHolder;
    [SerializeField] private bool isOrange;

    public void ShowPokemon(CharacterInfo character)
    {
        pokemonName.text = character.pokemonName;
        pokemonPortrait.sprite = character.icon;

        StartCoroutine(DoAnimation());
    }

    private IEnumerator DoAnimation()
    {
        float xPos = isOrange ? bgHolder.rect.width : -bgHolder.rect.width;

        bgHolder.anchoredPosition = new Vector2(xPos, bgHolder.anchoredPosition.y);

        yield return bgHolder.DOAnchorPosX(0, 0.4f).SetEase(Ease.OutBack).WaitForCompletion();

        yield return new WaitForSeconds(2f);

        yield return bgHolder.DOAnchorPosX(xPos, 0.4f).SetEase(Ease.InBack).WaitForCompletion();

        Destroy(gameObject);
    }
}
