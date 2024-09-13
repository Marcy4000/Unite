using DG.Tweening;
using TMPro;
using UnityEngine;

public class SurrenderTextbox : MonoBehaviour
{
    [SerializeField] private GameObject surrenderTextbox;
    [SerializeField] private TMP_Text surrenderText;

    private string surrenderTextBlue = "Your team surrendered, the opposing team won the game";
    private string surrenderTextOrange = "The opposing team surrendered, your team won the game";

    private void Start()
    {
        surrenderTextbox.SetActive(false);
    }

    public void ShowSurrenderTextbox(bool yourTeamSurrendered)
    {
        surrenderTextbox.SetActive(true);
        
        var rectTransform = surrenderTextbox.GetComponent<RectTransform>();

        rectTransform.anchoredPosition = new Vector2(0, -rectTransform.rect.height);
        rectTransform.DOAnchorPosY(0, 0.5f).SetEase(Ease.OutBack);

        surrenderText.text = yourTeamSurrendered ? surrenderTextBlue : surrenderTextOrange;
    }
}
