using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DraftBansShowcase : MonoBehaviour
{
    [SerializeField] private GameObject banShowcaseIconPrefab;
    [SerializeField] private GameObject middleIcon;
    [SerializeField] private Transform icons;

    [SerializeField] private GameObject showcaseHolder;
    [SerializeField] private GameObject showcaseBg;

    private List<DraftBanShowcaseIcon> banShowcaseIcons = new List<DraftBanShowcaseIcon>();

    private void Start()
    {
        showcaseHolder.SetActive(false);
        showcaseBg.SetActive(false);
    }

    public void ShowBans(List<CharacterInfo> bannedCharacters)
    {
        showcaseHolder.SetActive(true);
        showcaseBg.SetActive(true);

        foreach (Transform child in icons)
        {
            Destroy(child.gameObject);
        }

        banShowcaseIcons.Clear();

        for (int i = 0; i < bannedCharacters.Count; i++)
        {
            if (i == bannedCharacters.Count / 2)
            {
                Instantiate(middleIcon, icons);
            }

            GameObject icon = Instantiate(banShowcaseIconPrefab, icons);
            icon.GetComponent<DraftBanShowcaseIcon>().Initialize(bannedCharacters[i], i >= bannedCharacters.Count / 2);
            banShowcaseIcons.Add(icon.GetComponent<DraftBanShowcaseIcon>());
        }

        StartCoroutine(DoAnimation());
    }

    private IEnumerator DoAnimation()
    {
        showcaseHolder.transform.localScale = new Vector3(0, 1, 1);

        yield return showcaseHolder.transform.DOScaleX(1, 0.2f).WaitForCompletion();

        yield return new WaitForSeconds(0.1f);

        foreach (var icon in banShowcaseIcons)
        {
            icon.DoAnimation();
        }

        yield return new WaitForSeconds(3f);

        yield return showcaseHolder.transform.DOScaleX(0, 0.2f).WaitForCompletion();
        showcaseBg.SetActive(false);
        showcaseHolder.SetActive(false);
    }
}
