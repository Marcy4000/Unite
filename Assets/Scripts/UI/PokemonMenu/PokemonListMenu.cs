using System.Collections;
using UI.ThreeDimensional;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class PokemonListMenu : MonoBehaviour
{
    [SerializeField] private PokemonListIcons pokemonListIcons;
    [SerializeField] private PkmnListSideInfo pkmnListSideInfo;

    [SerializeField] private UIObject3D pokemonModel;

    private CharacterInfo currentPokemon;

    private AsyncOperationHandle<GameObject> modelHandle;

    private void OnEnable()
    {
        pokemonListIcons.Initialize(OnPokemonSelected);
    }

    private void OnPokemonSelected(CharacterInfo characterInfo)
    {
        pkmnListSideInfo.SetPokemonInfo(characterInfo);

        StartCoroutine(LoadPokemonModel(characterInfo));
    }

    private IEnumerator LoadPokemonModel(CharacterInfo characterInfo)
    {
        if (modelHandle.IsValid())
        {
            pokemonModel.ObjectPrefab = null;
            pokemonModel.imageComponent.color = Color.clear;
            Addressables.Release(modelHandle);
        }

        modelHandle = characterInfo.model.InstantiateAsync();

        yield return modelHandle;

        if (modelHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError("Failed to load model");
            yield break;
        }

        pokemonModel.ObjectPrefab = modelHandle.Result.transform;
        pokemonModel.imageComponent.color = Color.white;
    }

    private void OnDisable()
    {
        if (modelHandle.IsValid())
        {
            pokemonModel.ObjectPrefab = null;
            pokemonModel.imageComponent.color = Color.clear;
            Addressables.Release(modelHandle);
        }
    }
}
