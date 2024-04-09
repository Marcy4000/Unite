using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class CharacterSelectController : NetworkBehaviour
{
    private const float SELECTION_TIME = 30f;

    [SerializeField] private GameObject playerIconPrefab;
    [SerializeField] private Transform playerIconsHolder;

    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private Transform characterSpawnPoint;

    [SerializeField] private Transform pokemonSpawnPoint;
    private GameObject currentPokemon;

    [SerializeField] private TMP_Text timerText;

    private NetworkVariable<float> selectionTimer = new NetworkVariable<float>(SELECTION_TIME);
    private bool isLoading = false;
    private bool startTimer = false;

    private Lobby currentLobby;

    private void OnEnable()
    {
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName == "CharacterSelect")
        {
            if (IsServer)
            {
                selectionTimer.Value = SELECTION_TIME;
                startTimer = true;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        currentLobby = LobbyController.instance.Lobby;

        Player[] localTeamPlayers = GetTeamPlayers(LobbyController.instance.Player.Data["PlayerTeam"].Value == "Orange");
        foreach (var player in localTeamPlayers)
        {
            GameObject playerIcon = Instantiate(playerIconPrefab, playerIconsHolder);
            playerIcon.GetComponent<PlayerSelectionIcon>().Initialize(player);
            if (player.Id == LobbyController.instance.Player.Id)
            {
                SpawnPokemon(CharactersList.instance.GetCharacterFromString(player.Data["SelectedCharacter"].Value));
            }
        }

        foreach (var character in CharactersList.instance.Characters)
        {
            GameObject characterIcon = Instantiate(characterPrefab, characterSpawnPoint);
            characterIcon.GetComponent<CharacterSelectIcon>().Initialize(character);
            characterIcon.GetComponent<CharacterSelectIcon>().OnCharacterSelected += ChangeCharacter;
        }

        selectionTimer.OnValueChanged += UpdateTimerText;
    }

    private void UpdateTimerText(float previous, float current)
    {
        int time = Mathf.FloorToInt(current);
        if (time < 0)
        {
            time = 0;
        }
        timerText.text = time.ToString();
    }

    private void Update()
    {
        if (IsServer)
        {
            if (startTimer)
            {
                selectionTimer.Value -= Time.deltaTime;
                if (selectionTimer.Value <= 0 && !isLoading)
                {
                    ShowLoadingScreenRpc();
                    LobbyController.instance.LoadRemoat();
                    isLoading = true;
                }
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowLoadingScreenRpc()
    {
        LoadingScreen.Instance.ShowMatchLoadingScreen();
    }

    private void ChangeCharacter(CharacterInfo character)
    {
        LobbyController.instance.ChangePlayerCharacter(character.pokemonName);
        SpawnPokemon(character);
    }

    private Player[] GetTeamPlayers(bool orangeTeam)
    {
        List<Player> teamPlayers = new List<Player>();
        foreach (var player in currentLobby.Players)
        {
            if (player.Data["PlayerTeam"].Value == (orangeTeam ? "Orange" : "Blue"))
            {
                teamPlayers.Add(player);
            }
        }
        return teamPlayers.ToArray();
    }

    private void SpawnPokemon(CharacterInfo character)
    {
        if (currentPokemon != null)
        {
            Destroy(currentPokemon);
        }
        currentPokemon = Instantiate(character.model, pokemonSpawnPoint);
    }
}
