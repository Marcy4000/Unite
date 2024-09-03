using System;
using System.Collections.Generic;
using UnityEngine;

public class PokemonListIcons : MonoBehaviour
{
    [SerializeField] private GameObject pokemonIconPrefab;
    [SerializeField] private Transform pokemonIconParent;

    private List<CharacterSelectIcon> icons = new List<CharacterSelectIcon>();

    public void Initialize(Action<CharacterInfo> onClickAction)
    {
        foreach (var icon in icons)
        {
            Destroy(icon.gameObject);
        }

        icons.Clear();

        foreach (var pokemon in CharactersList.Instance.Characters)
        {
            var iconObj = Instantiate(pokemonIconPrefab, pokemonIconParent);
            var icon = iconObj.GetComponent<CharacterSelectIcon>();

            icon.Initialize(pokemon);
            icon.OnCharacterSelected += onClickAction;
            icons.Add(icon);
        }

        onClickAction(CharactersList.Instance.Characters[0]);
    }

}
