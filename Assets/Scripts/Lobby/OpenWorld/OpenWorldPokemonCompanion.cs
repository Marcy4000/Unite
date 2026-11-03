using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(OpenWorldTrainerAvatar))]
public sealed class OpenWorldPokemonCompanion : NetworkBehaviour
{
    [SerializeField] private OpenWorldPokemonFollower followerPrefab;

    private const string PreferenceKey = "OpenWorldLobby.Companion";
    private readonly NetworkVariable<short> selectedPokemon = new NetworkVariable<short>(-1);
    private AsyncOperationHandle<PokemonBase> pokemonHandle;
    private Coroutine loading;
    private OpenWorldPokemonFollower follower;
    private bool requestedInitialSelection;

    public event Action SelectionChanged;
    public short SelectedPokemonId => selectedPokemon.Value;
    public CharacterInfo SelectedPokemon => CharactersList.Instance != null
        ? CharactersList.Instance.GetCharacterFromID(SelectedPokemonId) : null;
    public string LoadStatus { get; private set; } = "Choose a Pokémon to explore the city with you.";
    public bool CanSelect => IsSpawned && IsOwner && IsClient && isActiveAndEnabled && CharactersList.Instance != null;

    public override void OnNetworkSpawn()
    {
        selectedPokemon.OnValueChanged += OnSelectionChanged;
        RefreshFollower();
        RequestPreferredSelection();
    }

    public override void OnGainedOwnership() => RequestPreferredSelection();

    public override void OnLostOwnership() => requestedInitialSelection = false;

    public override void OnNetworkDespawn()
    {
        selectedPokemon.OnValueChanged -= OnSelectionChanged;
        requestedInitialSelection = false;
        ClearFollower();
    }

    private void OnEnable()
    {
        if (IsSpawned) RefreshFollower();
    }

    private void OnDisable() => ClearFollower();

    private void Update()
    {
        if (!requestedInitialSelection) RequestPreferredSelection();
    }

    private void RequestPreferredSelection()
    {
        if (requestedInitialSelection || !CanSelect) return;
        string preferred = PlayerPrefs.GetString(PreferenceKey, "");
        short first = -1;
        var characters = CharactersList.Instance.Characters;
        for (int index = 0; index < characters.Length && index <= short.MaxValue; index++)
        {
            if (!IsSelectable(characters[index])) continue;
            if (first < 0) first = (short)index;
            if (characters[index].pokemonName != preferred) continue;
            first = (short)index;
            break;
        }
        if (first < 0) return;
        requestedInitialSelection = true;
        SelectPokemonRpc(first);
    }

    private static bool IsSelectable(CharacterInfo character) => character != null && !character.Hidden &&
        character.pokemon != null && character.pokemon.RuntimeKeyIsValid();

    public bool TrySelect(short characterId)
    {
        if (!CanSelect || !IsSelectable(CharactersList.Instance.GetCharacterFromID(characterId))) return false;
        PlayerPrefs.SetString(PreferenceKey, CharactersList.Instance.GetCharacterFromID(characterId).pokemonName);
        PlayerPrefs.Save();
        if (selectedPokemon.Value == characterId && follower == null && loading == null) RefreshFollower();
        SelectPokemonRpc(characterId);
        return true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SelectPokemonRpc(short characterId)
    {
        if (CharactersList.Instance != null && IsSelectable(CharactersList.Instance.GetCharacterFromID(characterId)))
            selectedPokemon.Value = characterId;
    }

    private void OnSelectionChanged(short previous, short current)
    {
        RefreshFollower();
        SelectionChanged?.Invoke();
    }

    private void RefreshFollower()
    {
        ClearFollower();
        if (!IsClient || !isActiveAndEnabled || followerPrefab == null || !IsSelectable(SelectedPokemon)) return;
        loading = StartCoroutine(LoadFollower(SelectedPokemon));
    }

    private IEnumerator LoadFollower(CharacterInfo character)
    {
        LoadStatus = $"Calling {character.pokemonName}…";
        pokemonHandle = Addressables.LoadAssetAsync<PokemonBase>(character.pokemon);
        yield return pokemonHandle;
        if (pokemonHandle.Status == AsyncOperationStatus.Succeeded)
        {
            var evolutions = pokemonHandle.Result.Evolutions;
            var model = evolutions != null && evolutions.Length > 0 ? evolutions[evolutions.Length - 1]?.newModel : null;
            if (model != null)
            {
                follower = Instantiate(followerPrefab, transform.position, transform.rotation);
                follower.name = $"{character.pokemonName} companion";
                SceneManager.MoveGameObjectToScene(follower.gameObject, gameObject.scene);
                follower.Initialize(transform, model);
                LoadStatus = $"{character.pokemonName} is your companion.";
                loading = null;
                yield break;
            }
        }
        LoadStatus = $"Couldn't load {character.pokemonName}. Select it again to retry.";
        Debug.LogWarning(LoadStatus, this);
        if (pokemonHandle.IsValid()) Addressables.Release(pokemonHandle);
        pokemonHandle = default;
        loading = null;
    }

    private void ClearFollower()
    {
        if (loading != null) StopCoroutine(loading);
        loading = null;
        if (follower != null)
        {
            follower.gameObject.SetActive(false);
            Destroy(follower.gameObject);
        }
        follower = null;
        if (pokemonHandle.IsValid()) Addressables.Release(pokemonHandle);
        pokemonHandle = default;
    }
}
