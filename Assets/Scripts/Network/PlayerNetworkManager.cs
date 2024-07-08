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

    private NetworkVariable<PlayerStats> playerStats = new NetworkVariable<PlayerStats>(writePerm: NetworkVariableWritePermission.Owner);
    private int killsSinceLastDeath = 0;
    private int pointsSinceLastDeath = 0;

    public PlayerStats PlayerStats => playerStats.Value;

    private float deathTimer = 5f;

    public override void OnNetworkSpawn()
    {
        //GameManager.instance.onGameStateChanged += HandleGameStateChanged;
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += HandleSceneLoaded;
        if (IsOwner)
        {
            playerStats.Value = new PlayerStats(LobbyController.Instance.Player.Id, 0, 0, 0, 0, 0, 0, 0);
            lobbyPlayerId.Value = LobbyController.Instance.Player.Id;
        }
        LobbyController.Instance.onLobbyUpdate += HandleLobbyUpdate;
        playerStats.OnValueChanged += HandlePlayerStatsChange;
    }

    public override void OnDestroy()
    {
        try
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
        }
        catch (System.Exception)
        {
            // Do nothing
        }

        LobbyController.Instance.onLobbyUpdate -= HandleLobbyUpdate;
        playerStats.OnValueChanged -= HandlePlayerStatsChange;
    }

    private void HandleLobbyUpdate(Lobby lobby)
    {
        if (string.IsNullOrWhiteSpace(lobbyPlayerId.Value.ToString()))
        {
            return;
        }

        try
        {
            if (LocalPlayer.Data.ContainsKey("PlayerTeam"))
            {
                orangeTeam = LocalPlayer.Data["PlayerTeam"].Value == "Orange";
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning(e.Message);
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

    private void HandlePlayerStatsChange(PlayerStats previous, PlayerStats updated)
    {
        if (playerManager != null)
        {
            playerManager.PlayerStats = updated;
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
                deathTimer = RespawnSystem.CalculateRespawnTime(playerManager.Pokemon.CurrentLevel, killsSinceLastDeath, pointsSinceLastDeath, GameManager.Instance.GameTime);
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
            if (IsOwner)
            {
                playerManager.MovesController.GameStarted();
            }
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

                    // THIS IS TEMPORARY, REMOVE ONCE IT'S FINISHED
                    MinimapManager.Instance.CreatePlayerIcon(player);

                    if (IsOwner)
                    {
                        player.Pokemon.OnDeath += OnPlayerDeath;
                        player.Pokemon.OnDamageTaken += OnPlayerTakeDamage;
                        player.Pokemon.onDamageDealt += OnPlayerDealDamage;
                        player.onGoalScored += OnGoalScored;
                        player.Pokemon.onOtherPokemonKilled += OnOtherPokemonKilled;

                        short pos = NumberEncoder.Base64ToShort(LobbyController.Instance.Player.Data["PlayerPos"].Value);
                        Transform spawnpoint = orangeTeam ? SpawnpointManager.Instance.GetOrangeTeamSpawnpoint(pos) : SpawnpointManager.Instance.GetBlueTeamSpawnpoint(pos);
                        playerManager.UpdatePosAndRotRPC(spawnpoint.position, spawnpoint.rotation);
                        playerManager.PlayerMovement.CanMove = false;
                    }
                }
            }
        }
    }

    private void OnPlayerDealDamage(ulong attacked, int amount)
    {
        Pokemon attackedPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[attacked].GetComponent<Pokemon>();

        if (attackedPokemon.Type != PokemonType.Player)
        {
            return;
        }

        playerStats.Value = new PlayerStats(lobbyPlayerId.Value.ToString(), playerStats.Value.kills, playerStats.Value.deaths, playerStats.Value.assists, playerStats.Value.score, playerStats.Value.damageDealt + (uint)amount, playerStats.Value.damageTaken, playerStats.Value.healingDone);
    }

    private void OnPlayerTakeDamage(DamageInfo info)
    {
        Pokemon attackerPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].GetComponent<Pokemon>();

        if (attackerPokemon.Type != PokemonType.Player)
        {
            return;
        }

        playerStats.Value = new PlayerStats(lobbyPlayerId.Value.ToString(), playerStats.Value.kills, playerStats.Value.deaths, playerStats.Value.assists, playerStats.Value.score, playerStats.Value.damageDealt, playerStats.Value.damageTaken + (uint)playerManager.Pokemon.CalculateDamage(info, attackerPokemon), playerStats.Value.healingDone);
    }

    private void OnOtherPokemonKilled(ulong killedId)
    {
        Pokemon attackedPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[killedId].GetComponent<Pokemon>();

        if (attackedPokemon.Type != PokemonType.Player)
        {
            return;
        }

        killsSinceLastDeath++;
        playerStats.Value = new PlayerStats(lobbyPlayerId.Value.ToString(), (ushort)(playerStats.Value.kills+1), playerStats.Value.deaths, playerStats.Value.assists, playerStats.Value.score, playerStats.Value.damageDealt, playerStats.Value.damageTaken, playerStats.Value.healingDone);
    }

    private void OnGoalScored(int amount)
    {
        amount = GameManager.Instance.FinalStretch ? amount * 2 : amount;

        pointsSinceLastDeath += amount;
        playerStats.Value = new PlayerStats(lobbyPlayerId.Value.ToString(), playerStats.Value.kills, playerStats.Value.deaths, playerStats.Value.assists, (ushort)(playerStats.Value.score + amount), playerStats.Value.damageDealt, playerStats.Value.damageTaken, playerStats.Value.healingDone);
    }

    private void OnPlayerDeath(DamageInfo info)
    {
        playerStats.Value = new PlayerStats(lobbyPlayerId.Value.ToString(), playerStats.Value.kills, (ushort)(playerStats.Value.deaths + 1), playerStats.Value.assists, playerStats.Value.score, playerStats.Value.damageDealt, playerStats.Value.damageTaken, playerStats.Value.healingDone);

        ShowKillRpc(info, !playerManager.OrangeTeam);
        deathTimer = RespawnSystem.CalculateRespawnTime(playerManager.Pokemon.CurrentLevel, killsSinceLastDeath, pointsSinceLastDeath, GameManager.Instance.GameTime);
        BattleUIManager.instance.ShowDeathScreen();

        killsSinceLastDeath = 0;
        pointsSinceLastDeath = 0;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowKillRpc(DamageInfo info, bool orangeTeam)
    {
        BattleUIManager.instance.ShowKill(info, playerManager.Pokemon);
    }
}
