using UnityEngine;
using UnityEngine.UI;

public class ProgressionMenu : MonoBehaviour
{
    [SerializeField] private EvolutionMenu evolutionMenu;
    [SerializeField] private MovesMenu movesMenu;

    [SerializeField] private Toggle[] menuToggles;

    private void OnEnable()
    {
        menuToggles[0].onValueChanged.AddListener(OnEvolutionMenuToggle);
        menuToggles[1].onValueChanged.AddListener(OnMovesMenuToggle);
    }

    private void OnDisable()
    {
        menuToggles[0].onValueChanged.RemoveListener(OnEvolutionMenuToggle);
        menuToggles[1].onValueChanged.RemoveListener(OnMovesMenuToggle);
    }

    private void OnEvolutionMenuToggle(bool value)
    {
        if (value)
        {
            evolutionMenu.gameObject.SetActive(true);
            movesMenu.gameObject.SetActive(false);
        }
    }

    private void OnMovesMenuToggle(bool value)
    {
        if (value)
        {
            evolutionMenu.gameObject.SetActive(false);
            movesMenu.gameObject.SetActive(true);
        }
    }

    public void HideMenu()
    {
        gameObject.SetActive(false);
    }

    public void InitializeMenus(PokemonBase pokemon, CharacterInfo characterInfo)
    {
        evolutionMenu.Initialize(pokemon, characterInfo);
        movesMenu.Initialize(pokemon);
    }
}
