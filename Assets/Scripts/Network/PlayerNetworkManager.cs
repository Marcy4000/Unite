using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerNetworkManager : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    private PlayerManager playerManager;
    public Player LocalPlayer { get => LobbyController.Instance.GetPlayerByID(lobbyPlayerId.Value.ToString()); }

    private NetworkVariable<FixedString32Bytes> lobbyPlayerId = new NetworkVariable<FixedString32Bytes>(writePerm: NetworkVariableWritePermission.Owner);

    private bool matchStarted = false;

    private bool orangeTeam = false;

    private float deathTimer = 5f;

    public override void OnNetworkSpawn()
    {
        //GameManager.instance.onGameStateChanged += HandleGameStateChanged;
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
        if (IsOwner)
        {
            lobbyPlayerId.Value = LobbyController.Instance.Player.Id;
        }
        LobbyController.Instance.onLobbyUpdate += HandleLobbyUpdate;
    }

    private void HandleLobbyUpdate(Lobby lobby)
    {
        if (string.IsNullOrWhiteSpace(lobbyPlayerId.Value.ToString()))
        {
            return;
        }

        if (LocalPlayer.Data.ContainsKey("PlayerTeam"))
        {
            orangeTeam = LocalPlayer.Data["PlayerTeam"].Value == "Orange";
        }
    }

    private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        string selectedMap = LobbyController.Instance.Lobby.Data["SelectedMap"].Value;
        if (sceneName.Equals(selectedMap))
        {
            GameManager.Instance.onGameStateChanged += HandleGameStateChanged;
        }
    }

    private void Update()
    {
        if (!IsOwner || playerManager == null)
        {
            return;
        }

        if (playerManager.PlayerState == PlayerState.Dead)
        {
            deathTimer -= Time.deltaTime;
            BattleUIManager.instance.UpdateDeathScreenTimer(Mathf.RoundToInt(deathTimer));

            if (deathTimer <= 0)
            {
                BattleUIManager.instance.HideDeathScreen();
                playerManager.Respawn();
                deathTimer = 5f;
            }
        }
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.Initialising && playerManager == null)
        {
            SpawnPlayerObject();
        }

        if (state == GameState.Playing && !matchStarted)
        {
            playerManager.PlayerMovement.CanMove = true;
            matchStarted = true;
        }

        if (state == GameState.Ended)
        {
            playerManager.PlayerMovement.CanMove = false;
        }
    }

    public void SpawnPlayerObject()
    {
        if (IsOwner)
        {
            bool currentTeam = LobbyController.Instance.Player.Data["PlayerTeam"].Value == "Orange";
            SpawnPlayerRpc(OwnerClientId, currentTeam);
        }
    }

    [Rpc(SendTo.Server)]
    private void SpawnPlayerRpc(ulong clientID, bool orangeTeam)
    {
        Transform spawnpoint = orangeTeam ? SpawnpointManager.Instance.GetOrangeTeamSpawnpoint() : SpawnpointManager.Instance.GetBlueTeamSpawnpoint();
        GameObject spawnedPlayer = Instantiate(playerPrefab, spawnpoint.position, spawnpoint.rotation);
        var spawnedPlayerNetworkObject = spawnedPlayer.GetComponent<NetworkObject>();
        spawnedPlayerNetworkObject.SpawnAsPlayerObject(clientID, true);
        OnPlayerSpawnedRpc(spawnedPlayerNetworkObject.NetworkObjectId, orangeTeam);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void OnPlayerSpawnedRpc(ulong networkID, bool orangeTeam)
    {
        PlayerManager[] players = FindObjectsOfType<PlayerManager>();
        foreach (PlayerManager player in players)
        {
            NetworkObject playerNetworkObject = player.GetComponent<NetworkObject>();

            if (playerNetworkObject.NetworkObjectId == networkID)
            {
                GameManager.Instance.AddPlayer(player);
                if (playerNetworkObject.OwnerClientId == OwnerClientId)
                {
                    playerManager = player;
                    player.ChangeCurrentTeam(orangeTeam);
                    player.Initialize();
                    if (IsOwner)
                    {
                        player.Pokemon.OnDeath += OnPlayerDeath;
                        short pos = NumberEncoder.Base64ToShort(LobbyController.Instance.Player.Data["PlayerPos"].Value);
                        Transform spawnpoint = orangeTeam ? SpawnpointManager.Instance.GetOrangeTeamSpawnpoint(pos) : SpawnpointManager.Instance.GetBlueTeamSpawnpoint(pos);
                        playerManager.UpdatePosAndRotRPC(spawnpoint.position, spawnpoint.rotation);
                        playerManager.PlayerMovement.CanMove = false;
                    }
                }
            }
        }
    }

    private void OnPlayerDeath(DamageInfo info)
    {
        ShowKillRpc(info, !playerManager.OrangeTeam);
        BattleUIManager.instance.ShowDeathScreen();
        deathTimer = 5f;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowKillRpc(DamageInfo info, bool orangeTeam)
    {
        BattleUIManager.instance.ShowKill(info, orangeTeam, playerManager.Pokemon);
    }
}
