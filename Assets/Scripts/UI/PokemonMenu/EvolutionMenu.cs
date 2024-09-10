using TMPro;
using UnityEngine;

public class EvolutionMenu : MonoBehaviour
{
    [SerializeField] private TMP_Text className;
    [SerializeField] private TMP_Text classDescription;

    [SerializeField] private Transform iconsHolder;
    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private GameObject arrowPrefab;

    [SerializeField] [TextArea] private string[] classDescriptions;

    public void Initialize(PokemonBase pokemon, CharacterInfo characterInfo)
    {
        className.text = characterInfo.pClass.ToString();
        classDescription.text = classDescriptions[(byte)characterInfo.pClass];

        foreach (Transform child in iconsHolder)
        {
            Destroy(child.gameObject);
        }

        PokemonEvolution[] evolutions = pokemon.Evolutions;

        for (int i = 0; i < evolutions.Length; i++)
        {
            PokemonEvolution evolution = evolutions[i];

            GameObject icon = Instantiate(iconPrefab, iconsHolder);
            icon.GetComponent<EvolutionInfoUI>().SetEvolutionInfo(evolution);

            if (i < evolutions.Length - 1)
            {
                GameObject arrow = Instantiate(arrowPrefab, iconsHolder);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (Transform child in iconsHolder)
        {
            Destroy(child.gameObject);
        }
    }
}
