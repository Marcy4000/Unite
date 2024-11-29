using System.Collections.Generic;
using UnityEngine;

public class DraftPicksShowcaser : MonoBehaviour
{
    [SerializeField] private GameObject showcaseIconPrefab;

    private List<CharacterInfo> picksToShow = new List<CharacterInfo>();

    public int PicksToShowCount => picksToShow.Count;

    public void AddPickToShow(CharacterInfo character)
    {
        picksToShow.Add(character);
    }

    public void ShowPicks()
    {
        foreach (CharacterInfo character in picksToShow)
        {
            DraftPickShowcaseIcon icon = Instantiate(showcaseIconPrefab, transform).GetComponent<DraftPickShowcaseIcon>();
            icon.ShowPokemon(character);
        }

        picksToShow.Clear();
    }
}
