using Unity.Netcode;
using UnityEngine;

public class PlayerNetworkManager : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    private GameObject spawnedPlayer;
    private PlayerManager playerManager;

    private float deathTimer = 5f;

    public override void OnNetworkSpawn()
    {
        GameManager.instance.onGameStateChanged += HandleGameStateChanged;
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
        if (state == GameState.Playing && spawnedPlayer == null)
        {
            SpawnPlayerObject();
        }
    }

    public void SpawnPlayerObject()
    {
        if (IsOwner && IsServer)
        {
            Transform spawnpoint = NetworkManagerUI.instance.teamToggle.isOn ? SpawnpointManager.Instance.GetOrangeTeamSpawnpoint() : SpawnpointManager.Instance.GetBlueTeamSpawnpoint();
            spawnedPlayer = Instantiate(playerPrefab, spawnpoint.position, spawnpoint.rotation);
            var spawnedPlayerNetworkObject = spawnedPlayer.GetComponent<NetworkObject>();
            spawnedPlayerNetworkObject.SpawnAsPlayerObject(OwnerClientId);
            OnPlayerSpawnedRpc(spawnedPlayerNetworkObject.NetworkObjectId, NetworkManagerUI.instance.teamToggle.isOn);
        }
        else if (IsOwner && !IsServer)
        {
            SpawnPlayerRpc(OwnerClientId, NetworkManagerUI.instance.teamToggle.isOn);
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
                GameManager.instance.AddPlayer(player);
                if (IsOwner)
                {
                    player.Pokemon.OnDeath += OnPlayerDeath;
                }
            }
        }   
    }

    private void OnPlayerDeath(DamageInfo info)
    {
        BattleUIManager.instance.ShowKill(info, !playerManager.OrangeTeam);
        BattleUIManager.instance.ShowDeathScreen();
        deathTimer = 5f;
    }
}
