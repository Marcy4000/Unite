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

    private CharacterInfo currentPokemon;

    private AsyncOperationHandle<GameObject> modelHandle;
    private GameObject modelInstance;

    private void OnEnable()
    {
        StartCoroutine(LoadPokemonBases());

        progressionMenu.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // Release the model asset
        if (modelHandle.IsValid())
        {
            Destroy(modelInstance);
            pokemonModel.ObjectPrefab = null;
            pokemonModel.gameObject.SetActive(false);
            Addressables.Release(modelHandle);
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
        pkmnListSideInfo.SetPokemonInfo(characterInfo);

        if (pokemons.Count == CharactersList.Instance.Characters.Length)
        {
            progressionMenu.InitializeMenus(pokemons[GetCharacterIndex(characterInfo)], characterInfo);
        }

        StartCoroutine(LoadPokemonModel(characterInfo));
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

    private IEnumerator LoadPokemonModel(CharacterInfo characterInfo)
    {
        // Release previous model if valid
        if (modelHandle.IsValid())
        {
            Destroy(modelInstance);
            pokemonModel.ObjectPrefab = null;
            pokemonModel.gameObject.SetActive(false);
            Addressables.Release(modelHandle);
        }

        // Load the model asset asynchronously
        modelHandle = Addressables.LoadAssetAsync<GameObject>(characterInfo.model);

        yield return modelHandle;

        if (modelHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError("Failed to load model");
            yield break;
        }

        // Instantiate the loaded GameObject
        modelInstance = Instantiate(modelHandle.Result);

        // Set the instantiated model as the prefab for the UI object
        pokemonModel.gameObject.SetActive(true);
        pokemonModel.ObjectPrefab = modelInstance.transform;
    }

    public void ShowProgressionMenu()
    {
        progressionMenu.gameObject.SetActive(true);
    }
}
