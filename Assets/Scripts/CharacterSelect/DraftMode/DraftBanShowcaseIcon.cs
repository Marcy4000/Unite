using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DraftBanShowcaseIcon : MonoBehaviour
{
    [SerializeField] private Image portrait, banIcon;
    private bool animateLeft;

    public void Initialize(CharacterInfo info, bool animateLeft)
    {
        if (info != null)
            portrait.sprite = info.portrait;

        this.animateLeft = animateLeft;

        portrait.rectTransform.localPosition += animateLeft ? new Vector3(-50, 0, 0) : new Vector3(50, 0, 0);
        portrait.color = new Color(1, 1, 1, 0f);

        banIcon.rectTransform.localScale = new Vector3(2f, 2f, 2f);
        banIcon.color = new Color(1, 1, 1, 0);
    }

    public void DoAnimation()
    {
        StartCoroutine(DoAnimationRoutine(animateLeft));
    }

    private IEnumerator DoAnimationRoutine(bool animateLeft)
    {
        portrait.rectTransform.localPosition += animateLeft ? new Vector3(-50, 0, 0) : new Vector3(50, 0, 0);
        portrait.color = new Color(1, 1, 1, 0f);

        banIcon.rectTransform.localScale = new Vector3(4f, 4f, 4f);
        banIcon.color = new Color(1, 1, 1, 0f);

        portrait.rectTransform.DOLocalMoveX(0, 0.4f).SetEase(Ease.OutBack);
        portrait.DOFade(1, 0.4f);

        yield return new WaitForSeconds(0.45f);

        banIcon.rectTransform.DOScale(1, 0.3f).SetEase(Ease.OutBack);
        banIcon.DOFade(1, 0.3f);
    }
}
