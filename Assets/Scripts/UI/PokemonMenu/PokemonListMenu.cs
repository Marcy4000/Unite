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

    private CharacterInfo currentPokemon;

    private AsyncOperationHandle<GameObject> modelHandle;

    private void OnEnable()
    {
        StartCoroutine(LoadPokemonBases());

        progressionMenu.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (modelHandle.IsValid())
        {
            pokemonModel.ObjectPrefab = null;
            pokemonModel.gameObject.SetActive(false);
            Addressables.Release(modelHandle);
        }

        foreach (var pokemon in pokemons)
        {
            if (pokemon == null)
            {
                continue;
            }
            Addressables.Release(pokemon);
        }

        pokemons.Clear();
    }

    private IEnumerator LoadPokemonBases()
    {
        pokemons.Clear();

        LoadingScreen.Instance.ShowGenericLoadingScreen();

        foreach (var pokemonInfo in CharactersList.Instance.Characters)
        {
            AsyncOperationHandle<PokemonBase> handle = Addressables.LoadAssetAsync<PokemonBase>(pokemonInfo.pokemon);

            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                pokemons.Add(handle.Result);
            }
            else
            {
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
        if (modelHandle.IsValid())
        {
            pokemonModel.ObjectPrefab = null;
            pokemonModel.gameObject.SetActive(false);
            Addressables.Release(modelHandle);
        }

        modelHandle = characterInfo.model.InstantiateAsync();

        yield return modelHandle;

        if (modelHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError("Failed to load model");
            yield break;
        }

        pokemonModel.gameObject.SetActive(true);
        pokemonModel.ObjectPrefab = modelHandle.Result.transform;
    }

    public void ShowProgressionMenu()
    {
        progressionMenu.gameObject.SetActive(true);
    }
}
