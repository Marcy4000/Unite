using UnityEngine;
using UnityEngine.UI;

public class MapWildPokemon : MonoBehaviour
{
    [SerializeField] private Image icon;

    private MinimapIcon minimapIcon;

    private WildPokemonSpawner wildPokemon;

    public MinimapIcon MinimapIcon => minimapIcon;

    public void Initialize(WildPokemonSpawner wildPokemon)
    {
        minimapIcon = GetComponent<MinimapIcon>();
        this.wildPokemon = wildPokemon;
        minimapIcon.SetTarget(wildPokemon.transform);

        wildPokemon.OnShouldDestroyIcon += DestroyIcon;
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

    private void DestroyIcon()
    {
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        wildPokemon.OnShouldDestroyIcon -= DestroyIcon;
    }
}
