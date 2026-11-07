using System.Collections;
using System.Collections.Generic;
using UI.ThreeDimensional;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class PokemonListMenu : MonoBehaviour
{
    [SerializeField] private PokemonListIcons pokemonListIcons;
    [SerializeField] private PkmnListSideInfo pkmnListSideInfo;
    [SerializeField] private ProgressionMenu progressionMenu;

    [SerializeField] private UIObject3D pokemonModel;

    private List<PokemonBase> pokemons = new List<PokemonBase>();
    private List<AsyncOperationHandle<PokemonBase>> pokemonBaseHandles = new List<AsyncOperationHandle<PokemonBase>>();

    [SerializeField] private UnityEngine.UI.Button favouriteButton;
    private int selectedIndex = -1;
    private GameObject modelInstance;

    private void SetFavourite()
    {
        CharactersList.Instance.SetFavouritePokemon(selectedIndex);
        RefreshFavouriteButton();
    }

    private void RefreshFavouriteButton()
    {
        if (favouriteButton != null)
            favouriteButton.interactable = selectedIndex >= 0 &&
                CharactersList.IsPlayable(CharactersList.Instance.Characters[selectedIndex]) &&
                selectedIndex != CharactersList.Instance.FavouritePokemonIndex;
    }

    private void OnEnable()
    {
        selectedIndex = -1;
        if (favouriteButton != null) favouriteButton.onClick.AddListener(SetFavourite);
        CharactersList.Instance.FavouritePokemonChanged += RefreshFavouriteButton;
        RefreshFavouriteButton();
        StartCoroutine(LoadPokemonBases());

        progressionMenu.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (favouriteButton != null) favouriteButton.onClick.RemoveListener(SetFavourite);
        if (CharactersList.Instance != null) CharactersList.Instance.FavouritePokemonChanged -= RefreshFavouriteButton;
        StopAllCoroutines();
        // Release the model asset
        if (modelInstance != null)
        {
            Destroy(modelInstance);
            pokemonModel.ObjectPrefab = null;
        }

        // Release all loaded PokemonBase assets
        foreach (var handle in pokemonBaseHandles)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        pokemons.Clear();
        pokemonBaseHandles.Clear();
    }

    private IEnumerator LoadPokemonBases()
    {
        pokemons.Clear();
        pokemonBaseHandles.Clear();

        LoadingScreen.Instance.ShowGenericLoadingScreen();

        // Load all Pokémon base assets asynchronously
        foreach (var pokemonInfo in CharactersList.Instance.Characters)
        {
            AsyncOperationHandle<PokemonBase> handle = Addressables.LoadAssetAsync<PokemonBase>(pokemonInfo.pokemon);
            pokemonBaseHandles.Add(handle);

            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                pokemons.Add(handle.Result);
            }
            else
            {
                Debug.LogError($"Failed to load Pokémon {pokemonInfo.pokemon}");
                pokemons.Add(null);
            }
        }

        pokemonListIcons.Initialize(OnPokemonSelected);

        LoadingScreen.Instance.HideGenericLoadingScreen();
    }

    private void OnPokemonSelected(CharacterInfo characterInfo)
    {
        selectedIndex = GetCharacterIndex(characterInfo);
        RefreshFavouriteButton();
        pkmnListSideInfo.SetPokemonInfo(characterInfo);

        if (pokemons.Count == CharactersList.Instance.Characters.Length)
        {
            progressionMenu.InitializeMenus(pokemons[GetCharacterIndex(characterInfo)], characterInfo);
        }

        LoadPokemonModel(characterInfo);
    }

    private int GetCharacterIndex(CharacterInfo info)
    {
        for (int i = 0; i < CharactersList.Instance.Characters.Length; i++)
        {
            if (CharactersList.Instance.Characters[i] == info)
            {
                return i;
            }
        }

        return -1;
    }

    private void LoadPokemonModel(CharacterInfo characterInfo)
    {
        if (modelInstance != null)
        {
            Destroy(modelInstance);
        }

        PokemonBase pokemon = pokemons[GetCharacterIndex(characterInfo)];

        if (pokemon != null) {
            modelInstance = Instantiate(pokemon.Evolutions[pokemon.Evolutions.Length - 1].newModel);
            pokemonModel.ObjectPrefab = modelInstance.transform;
        }
    }

    public void ShowProgressionMenu()
    {
        progressionMenu.gameObject.SetActive(true);
    }
}
