using UnityEngine;
using UnityEngine.UI;

public class MapObjective : MonoBehaviour
{
    [SerializeField] private Image icon;

    private MinimapIcon minimapIcon;

    private WildPokemon wildPokemon;

    public MinimapIcon MinimapIcon => minimapIcon;

    public void Initialize(WildPokemon wildPokemon)
    {
        minimapIcon = GetComponent<MinimapIcon>();
        this.wildPokemon = wildPokemon;
        minimapIcon.SetTarget(wildPokemon.transform);

        wildPokemon.Pokemon.OnDeath += (info) => { Destroy(gameObject); };
    }

    public void HideIcon()
    {
        icon.gameObject.SetActive(false);
    }

    public void ShowIcon()
    {
        icon.gameObject.SetActive(true);
    }

    public void SetVisibility(bool isVisible)
    {
        icon.gameObject.SetActive(isVisible);
    }
}
