using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerNetworkManager : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    private GameObject spawnedPlayer;
    private PlayerManager playerManager;
    private Player localPlayer;

    private float deathTimer = 5f;

    public override void OnNetworkSpawn()
    {
        //GameManager.instance.onGameStateChanged += HandleGameStateChanged;
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
        if (IsOwner)
        {
            localPlayer = LobbyController.Instance.Player;
            LobbyController.Instance.onLobbyUpdate += HandleLobbyUpdate;
        }
    }

    private void HandleLobbyUpdate(Lobby lobby)
    {
        localPlayer = lobby.Players.Find(x => x.Id == localPlayer.Id);
    }

    private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName.Equals("RemoatStadium"))
        {
            GameManager.Instance.onGameStateChanged += HandleGameStateChanged;
        }
    }

    private void Update()
    {
        if (!IsOwner || spawnedPlayer == null)
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
        if (state == GameState.Initialising && spawnedPlayer == null)
        {
            SpawnPlayerObject();
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
        spawnedPlayer = Instantiate(playerPrefab, spawnpoint.position, spawnpoint.rotation);
        var spawnedPlayerNetworkObject = spawnedPlayer.GetComponent<NetworkObject>();
        spawnedPlayerNetworkObject.SpawnAsPlayerObject(clientID);
        OnPlayerSpawnedRpc(spawnedPlayerNetworkObject.NetworkObjectId, orangeTeam);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void OnPlayerSpawnedRpc(ulong networkID, bool orangeTeam)
    {
        PlayerManager[] players = FindObjectsOfType<PlayerManager>();
        foreach (PlayerManager player in players)
        {
            if (player.GetComponent<NetworkObject>().NetworkObjectId == networkID)
            {
                spawnedPlayer = player.gameObject;
                playerManager = player;
                player.ChangeCurrentTeam(orangeTeam);
                GameManager.Instance.AddPlayer(player);
                if (IsOwner)
                {
                    player.Pokemon.OnDeath += OnPlayerDeath;
                    Transform spawnpoint = orangeTeam ? SpawnpointManager.Instance.GetOrangeTeamSpawnpoint() : SpawnpointManager.Instance.GetBlueTeamSpawnpoint();
                    player.transform.position = spawnpoint.position;
                    player.transform.rotation = spawnpoint.rotation;
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
