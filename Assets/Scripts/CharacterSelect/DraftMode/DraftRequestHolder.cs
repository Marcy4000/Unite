using DG.Tweening;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DraftRequestHolder : MonoBehaviour
{
    [SerializeField] private GameObject holder, banUIHolder;
    [SerializeField] private Image bannedCharacterIcon;
    [SerializeField] private TMP_Text requestTypeText, requestText;

    private void Start()
    {
        holder.SetActive(false);
    }

    public void ShowRequest(CharacterInfo requestedCharacter, bool isBanRequest)
    {
        banUIHolder.SetActive(isBanRequest);
        bannedCharacterIcon.sprite = requestedCharacter.portrait;
        requestTypeText.text = isBanRequest ? "Ban Request" : "Switch Request";
        requestText.text = requestedCharacter.pokemonName;

        StartCoroutine(DoAnimation());
    }

    private IEnumerator DoAnimation()
    {
        holder.SetActive(true);

        RectTransform rect = holder.GetComponent<RectTransform>();
        rect.localScale = new Vector3(0.0f, 0.0f, 0.0f);

        yield return rect.DOScale(1.0f, 0.3f).SetEase(Ease.OutBack).WaitForCompletion();

        yield return new WaitForSeconds(2.5f);

        yield return rect.DOScale(0.0f, 0.3f).SetEase(Ease.InBack).WaitForCompletion();

        holder.SetActive(false);
    }
}
