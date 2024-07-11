using System.Collections.Generic;
using UnityEngine;

public class CharacterSelectorUI : MonoBehaviour
{
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private Transform characterSpawnPoint;

    public List<CharacterSelectIcon> InitializeUI()
    {
        List<CharacterSelectIcon> characterSelectIcons = new List<CharacterSelectIcon>();

        foreach (var character in CharactersList.instance.Characters)
        {
            GameObject characterIcon = Instantiate(characterPrefab, characterSpawnPoint);
            characterIcon.GetComponent<CharacterSelectIcon>().Initialize(character);
            characterSelectIcons.Add(characterIcon.GetComponent<CharacterSelectIcon>());
        }

        return characterSelectIcons;
    }
}
