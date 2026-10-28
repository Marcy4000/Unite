using JSAM;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Player = Unite.Multiplayer.GameSessionPlayer;
using Lobby = Unite.Multiplayer.GameSessionView;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class PlayerNetworkManager : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    private PlayerManager playerManager;
    public Player LocalPlayer => LobbyController.Instance != null
        ? LobbyController.Instance.GetPlayerByID(lobbyPlayerId.Value.ToString()) : null;
    private NetworkSceneManager subscribedSceneManager;
    private LobbyController subscribedLobbyController;
    private GameManager subscribedGameManager;

    private NetworkVariable<FixedString32Bytes> lobbyPlayerId = new NetworkVariable<FixedString32Bytes>(writePerm: NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> isInResultScreen = new NetworkVariable<bool>(false, writePerm: NetworkVariableWritePermission.Owner);

    public bool IsInResultScreen => isInResultScreen.Value;

    private bool matchStarted = false;

    private Team team;

    private NetworkVariable<PlayerStats> playerStats = new NetworkVariable<PlayerStats>(writePerm: NetworkVariableWritePermission.Owner);
    private int killsSinceLastDeath = 0;
    private int pointsSinceLastDeath = 0;

    public PlayerStats PlayerStats => playerStats.Value;

    private const float DefaultDeathTimer = 5f;
    private float localDeathTimer = DefaultDeathTimer;
    private NetworkVariable<float> deathTimer = new NetworkVariable<float>(DefaultDeathTimer, writePerm:NetworkVariableWritePermission.Owner);
    public float DeathTimer => deathTimer.Value;
    public event System.Action<float> OnDeathTimerChanged;
    public event System.Action<PlayerStats> OnPlayerStatsChanged;

    public PlayerManager Player => playerManager;

    public override void OnNetworkSpawn()
    {
        subscribedSceneManager = NetworkManager.SceneManager;
        subscribedSceneManager.OnLoadEventCompleted += HandleSceneLoaded;

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;

        if (IsOwner)
        {
            ResetMatchStats();
            lobbyPlayerId.Value = LobbyController.Instance.Player.Id;
        }
        subscribedLobbyController = LobbyController.Instance;
        subscribedLobbyController.onLobbyUpdate += HandleLobbyUpdate;
        playerStats.OnValueChanged += HandlePlayerStatsChange;
        deathTimer.OnValueChanged += HandleDeathTimerChange;

        if (!subscribedLobbyController.PlayerNetworkManagers.Contains(this))
            subscribedLobbyController.PlayerNetworkManagers.Add(this);
        InitializeBotRoster();
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeEvents();
    }

    public override void OnDestroy()
    {
        UnsubscribeEvents();
        base.OnDestroy();
    }

    private void UnsubscribeEvents()
    {
        UnsubscribeBotRoster();
        if (subscribedSceneManager != null)
        {
            subscribedSceneManager.OnLoadEventCompleted -= HandleSceneLoaded;
            subscribedSceneManager = null;
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        if (subscribedLobbyController != null)
        {
            subscribedLobbyController.onLobbyUpdate -= HandleLobbyUpdate;
            subscribedLobbyController.PlayerNetworkManagers.Remove(this);
            subscribedLobbyController = null;
        }
        if (subscribedGameManager != null)
        {
            subscribedGameManager.onGameStateChanged -= HandleGameStateChanged;
            subscribedGameManager = null;
        }
        playerStats.OnValueChanged -= HandlePlayerStatsChange;
        deathTimer.OnValueChanged -= HandleDeathTimerChange;

        // Rimuovi la sottoscrizione all'evento OnRespawn se necessario
        if (playerManager != null)
        {
            var pokemon = playerManager.Pokemon;
            if (pokemon != null)
            {
                pokemon.OnDeath -= OnPlayerDeath;
                pokemon.OnDamageTaken -= OnPlayerTakeDamage;
                pokemon.OnDamageDealt -= OnPlayerDealDamage;
                pokemon.OnHealingDone -= OnPlayerHeal;
                pokemon.OnOtherPokemonKilled -= OnOtherPokemonKilled;
            }
            playerManager.onGoalScored -= OnGoalScored;
            playerManager.OnRespawn -= OnPlayerRespawned;

            // Rimuovi la sottoscrizione all'evento OnMoveLearned se necessario
            if (playerManager.MovesController != null)
            {
                playerManager.MovesController.OnMoveLearned -= OnMoveLearnedFromController;
            }
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsOwner)
        {
            return;
        }

        if (scene.name.Equals(CharactersList.Instance.GetCurrentLobbyMap().resultScreenScene))
        {
            isInResultScreen.Value = true;
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        if (!IsOwner)
        {
            return;
        }

        if (scene.name.Equals(CharactersList.Instance.GetCurrentLobbyMap().resultScreenScene))
        {
            ResetMatchStats();
        }
    }

    private void ResetMatchStats()
    {
        // This coordinator survives a return to the same lobby. Streaks and
        // respawn timers belong to a match just like the visible scoreboard.
        matchStarted = false;
        isInResultScreen.Value = false;
        killsSinceLastDeath = 0;
        pointsSinceLastDeath = 0;
        localDeathTimer = DefaultDeathTimer;
        deathTimer.Value = localDeathTimer;
        playerStats.Value = new PlayerStats(LobbyController.Instance.Player.Id, 0, 0, 0, 0, 0, 0, 0);
    }

    private void HandleLobbyUpdate(Lobby lobby)
    {
        if (string.IsNullOrWhiteSpace(lobbyPlayerId.Value.ToString()))
        {
            return;
        }

        try
        {
            if (LocalPlayer != null && LocalPlayer.Data.ContainsKey("PlayerTeam"))
            {
                team = TeamMember.GetTeamFromString(LocalPlayer.Data["PlayerTeam"].Value);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning(e.Message);
        }
    }

    private void HandleSceneLoaded(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        MapInfo selectedMap = CharactersList.Instance.GetCurrentLobbyMap();
        if (sceneName.Equals(selectedMap.sceneName) && GameManager.Instance != null &&
            subscribedGameManager != GameManager.Instance)
        {
            if (subscribedGameManager != null)
                subscribedGameManager.onGameStateChanged -= HandleGameStateChanged;
            subscribedGameManager = GameManager.Instance;
            subscribedGameManager.onGameStateChanged += HandleGameStateChanged;
        }
    }

    private void HandlePlayerStatsChange(PlayerStats previous, PlayerStats updated)
    {
        if (playerManager != null)
        {
            playerManager.PlayerStats = updated;
        }
        OnPlayerStatsChanged?.Invoke(updated);
    }

    private void HandleDeathTimerChange(float previous, float updated) => OnDeathTimerChanged?.Invoke(updated);

    private void OnPlayerRespawned()
    {
        localDeathTimer = RespawnSystem.CalculateRespawnTime(playerManager.Pokemon.CurrentLevel, killsSinceLastDeath, pointsSinceLastDeath, GameManager.Instance.MAX_GAME_TIME - GameManager.Instance.GameTime);
        deathTimer.Value = localDeathTimer;
        BattleUIManager.instance.HideDeathScreen();
    }

    private void Update()
    {
        TickBotRoster();
        if (!IsOwner || playerManager == null)
        {
            return;
        }

        if (playerManager.PlayerState == PlayerState.Dead)
        {
            localDeathTimer -= Time.deltaTime;
            deathTimer.Value = localDeathTimer;
            BattleUIManager.instance.UpdateDeathScreenTimer(Mathf.RoundToInt(localDeathTimer));

            if (localDeathTimer <= 0)
            {
                BattleUIManager.instance.HideDeathScreen();
                playerManager.Respawn();
                localDeathTimer = RespawnSystem.CalculateRespawnTime(playerManager.Pokemon.CurrentLevel, killsSinceLastDeath, pointsSinceLastDeath, GameManager.Instance.MAX_GAME_TIME - GameManager.Instance.GameTime);
                deathTimer.Value = localDeathTimer;
            }
        }
        // Rimosso il controllo duplicato per il respawn anticipato, ora gestito da OnRespawn
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.Initialising && playerManager == null)
        {
            SpawnPlayerObject();
        }

        if (state == GameState.Playing && !matchStarted && playerManager != null)
        {
            playerManager.PlayerMovement.CanMove = true;
            if (IsOwner)
            {
                playerManager.MovesController.GameStarted();
            }
            matchStarted = true;
        }

        if (state == GameState.Ended && playerManager != null)
        {
            playerManager.PlayerMovement.CanMove = false;
        }
    }

    public void SpawnPlayerObject()
    {
        if (IsOwner)
        {
            Team currentTeam = LobbyController.Instance.GetLocalPlayerTeam();
            SpawnPlayerRpc(OwnerClientId, currentTeam);
        }
    }

    [Rpc(SendTo.Server)]
    private void SpawnPlayerRpc(ulong clientID, Team team)
    {
        Transform spawnpoint = SpawnpointManager.Instance.GetSpawnpoint(team);
        GameObject spawnedPlayer = Instantiate(playerPrefab, spawnpoint.position, spawnpoint.rotation);
        var spawnedPlayerNetworkObject = spawnedPlayer.GetComponent<NetworkObject>();
        spawnedPlayerNetworkObject.SpawnAsPlayerObject(clientID, true);
        OnPlayerSpawnedRpc(spawnedPlayerNetworkObject.NetworkObjectId, team);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void OnPlayerSpawnedRpc(ulong networkID, Team team)
    {
        StartCoroutine(BindSpawnedPlayer(networkID, team));
    }

    private IEnumerator BindSpawnedPlayer(ulong networkID, Team team)
    {
        var match = GameManager.Instance;
        float deadline = Time.realtimeSinceStartup + 15;
        NetworkObject avatar = null;
        // NGO spawn replication and scene-owned presentation can arrive on
        // different frames. Never lose initialization because one scene scan ran early.
        while (IsSpawned && match != null && GameManager.Instance == match && Time.realtimeSinceStartup < deadline)
        {
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkID, out avatar) &&
                avatar != null && avatar.IsSpawned && MinimapManager.Instance != null) break;
            yield return null;
        }
        if (!IsSpawned || match == null || GameManager.Instance != match || avatar == null || !avatar.IsSpawned) yield break;
        var player = avatar.GetComponent<PlayerManager>();
        if (player == null || player.IsBot || avatar.OwnerClientId != OwnerClientId || playerManager == player) yield break;
        playerManager = player;
        matchStarted = false;
        // Also reset at the next avatar's initialization, before move tracking
        // starts, so a new match never depends on the previous results flow.
        if (IsOwner) ResetMatchStats();
        player.ChangeCurrentTeam(team);
        player.Initialize();
        match.AddPlayer(player);
        MinimapManager.Instance.CreatePlayerIcon(player);
        if (!IsOwner) yield break;
        player.Pokemon.OnDeath += OnPlayerDeath;
        player.Pokemon.OnDamageTaken += OnPlayerTakeDamage;
        player.Pokemon.OnDamageDealt += OnPlayerDealDamage;
        player.Pokemon.OnHealingDone += OnPlayerHeal;
        player.onGoalScored += OnGoalScored;
        player.Pokemon.OnOtherPokemonKilled += OnOtherPokemonKilled;
        player.OnRespawn += OnPlayerRespawned;
        if (player.MovesController != null)
        {
            SubscribeToMoveEvents();
            player.MovesController.OnMoveLearned += OnMoveLearnedFromController;
        }
        short pos = NumberEncoder.FromBase64<short>(LobbyController.Instance.Player.Data["PlayerPos"].Value);
        Transform spawnpoint = SpawnpointManager.Instance.GetSpawnpoint(team, pos);
        player.UpdatePosAndRotRPC(spawnpoint.position, spawnpoint.rotation);
        player.PlayerMovement.CanMove = false;
    }

    private void OnPlayerDealDamage(ulong attacked, DamageInfo damage)
    {
        Pokemon attackedPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[attacked].GetComponent<Pokemon>();

        if (attackedPokemon.Type != PokemonType.Player)
        {
            return;
        }

        int amount = attackedPokemon.CalculateDamage(damage, playerManager.Pokemon);

        var currentStats = playerStats.Value;
        playerStats.Value = new PlayerStats(
            lobbyPlayerId.Value.ToString(),
            currentStats.kills, currentStats.deaths, currentStats.assists, currentStats.score,
            currentStats.damageDealt + (uint)amount, currentStats.damageTaken, currentStats.healingDone,
            currentStats.moveA, currentStats.moveB, currentStats.uniteMove,
            currentStats.basicAttackName.ToString(), currentStats.battleItem,
            currentStats.moveAUpgraded, currentStats.moveBUpgraded, currentStats.uniteMoveUpgraded
        );
    }

    private void OnPlayerHeal(int amount)
    {
        if (!IsOwner || amount <= 0) return;
        var currentStats = playerStats.Value;
        playerStats.Value = new PlayerStats(
            lobbyPlayerId.Value.ToString(),
            currentStats.kills, currentStats.deaths, currentStats.assists, currentStats.score,
            currentStats.damageDealt, currentStats.damageTaken, currentStats.healingDone + (uint)amount,
            currentStats.moveA, currentStats.moveB, currentStats.uniteMove,
            currentStats.basicAttackName.ToString(), currentStats.battleItem,
            currentStats.moveAUpgraded, currentStats.moveBUpgraded, currentStats.uniteMoveUpgraded
        );
    }

    private void OnPlayerTakeDamage(DamageInfo info)
    {
        Pokemon attackerPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[info.attackerId].GetComponent<Pokemon>();

        if (attackerPokemon.Type != PokemonType.Player)
        {
            return;
        }

        var currentStats = playerStats.Value;
        playerStats.Value = new PlayerStats(
            lobbyPlayerId.Value.ToString(),
            currentStats.kills, currentStats.deaths, currentStats.assists, currentStats.score,
            currentStats.damageDealt, currentStats.damageTaken + (uint)playerManager.Pokemon.CalculateDamage(info, attackerPokemon), currentStats.healingDone,
            currentStats.moveA, currentStats.moveB, currentStats.uniteMove,
            currentStats.basicAttackName.ToString(), currentStats.battleItem,
            currentStats.moveAUpgraded, currentStats.moveBUpgraded, currentStats.uniteMoveUpgraded
        );
    }

    private void OnOtherPokemonKilled(ulong killedId)
    {
        Pokemon attackedPokemon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[killedId].GetComponent<Pokemon>();

        if (attackedPokemon.Type != PokemonType.Player)
        {
            return;
        }

        killsSinceLastDeath++;
        var currentStats = playerStats.Value;
        playerStats.Value = new PlayerStats(
            lobbyPlayerId.Value.ToString(),
            (ushort)(currentStats.kills + 1), currentStats.deaths, currentStats.assists, currentStats.score,
            currentStats.damageDealt, currentStats.damageTaken, currentStats.healingDone,
            currentStats.moveA, currentStats.moveB, currentStats.uniteMove,
            currentStats.basicAttackName.ToString(), currentStats.battleItem,
            currentStats.moveAUpgraded, currentStats.moveBUpgraded, currentStats.uniteMoveUpgraded
        );
    }

    private void OnGoalScored(int amount)
    {
        pointsSinceLastDeath += amount;
        var currentStats = playerStats.Value;
        playerStats.Value = new PlayerStats(
            lobbyPlayerId.Value.ToString(),
            currentStats.kills, currentStats.deaths, currentStats.assists, (ushort)(currentStats.score + amount),
            currentStats.damageDealt, currentStats.damageTaken, currentStats.healingDone,
            currentStats.moveA, currentStats.moveB, currentStats.uniteMove,
            currentStats.basicAttackName.ToString(), currentStats.battleItem,
            currentStats.moveAUpgraded, currentStats.moveBUpgraded, currentStats.uniteMoveUpgraded
        );
    }

    private void OnPlayerDeath(DamageInfo info)
    {
        var currentStats = playerStats.Value;
        playerStats.Value = new PlayerStats(
            lobbyPlayerId.Value.ToString(),
            currentStats.kills, (ushort)(currentStats.deaths + 1), currentStats.assists, currentStats.score,
            currentStats.damageDealt, currentStats.damageTaken, currentStats.healingDone,
            currentStats.moveA, currentStats.moveB, currentStats.uniteMove,
            currentStats.basicAttackName.ToString(), currentStats.battleItem,
            currentStats.moveAUpgraded, currentStats.moveBUpgraded, currentStats.uniteMoveUpgraded
        );

        ShowKillRpc(info);
        localDeathTimer = RespawnSystem.CalculateRespawnTime(playerManager.Pokemon.CurrentLevel, killsSinceLastDeath, pointsSinceLastDeath, GameManager.Instance.MAX_GAME_TIME - GameManager.Instance.GameTime);
        deathTimer.Value = localDeathTimer;
        BattleUIManager.instance.ShowDeathScreen();

        AudioManager.PlaySound(DefaultAudioSounds.Play_UI_Pokemon_Dead);

        killsSinceLastDeath = 0;
        pointsSinceLastDeath = 0;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowKillRpc(DamageInfo info)
    {
        BattleUIManager.instance.ShowKill(info, playerManager.Pokemon, null);
    }

    #region Move Tracking

    private void SubscribeToMoveEvents()
    {
        if (playerManager?.MovesController == null) return;
        
        // We'll hook into the MovesController to track move learning
        // This will be implemented through polling initially since MovesController doesn't have events for move learning
        InitializePlayerMoves();
    }

    private void InitializePlayerMoves()
    {
        if (playerManager?.MovesController == null) return;
        
        // Get pokemon name for basic attack
        string pokemonName = playerManager.Pokemon?.name ?? "";
        if (pokemonName.Contains("(Clone)"))
        {
            pokemonName = pokemonName.Replace("(Clone)", "").Trim();
        }

        // Get battle item from lobby data
        AvailableBattleItems battleItemType = AvailableBattleItems.None;
        if (LobbyController.Instance.Player.Data.ContainsKey("BattleItem"))
        {
            if (int.TryParse(LobbyController.Instance.Player.Data["BattleItem"].Value, out int battleItemId))
            {
                BattleItemAsset selectedBattleItem = CharactersList.Instance.GetBattleItemByID(battleItemId);
                if (selectedBattleItem != null)
                {
                    battleItemType = selectedBattleItem.battleItemType;
                }
            }
        }

        // Update player stats with initial move data
        UpdatePlayerStatsWithMoves(
            AvailableMoves.LockedMove, // moveA starts as locked
            AvailableMoves.LockedMove, // moveB starts as locked  
            AvailableMoves.LockedMove, // uniteMove starts as locked
            pokemonName,
            battleItemType,
            false, false, false // no upgrades initially
        );
    }

    public void UpdatePlayerStatsWithMoves(AvailableMoves moveA, AvailableMoves moveB, AvailableMoves uniteMove,
                                          string basicAttackName, AvailableBattleItems battleItem,
                                          bool moveAUpgraded, bool moveBUpgraded, bool uniteMoveUpgraded)
    {
        if (!IsOwner) return;

        var currentStats = playerStats.Value;
        
        playerStats.Value = new PlayerStats(
            currentStats.playerId.ToString(),
            currentStats.kills, currentStats.deaths, currentStats.assists, currentStats.score,
            currentStats.damageDealt, currentStats.damageTaken, currentStats.healingDone,
            moveA, moveB, uniteMove, basicAttackName, battleItem,
            moveAUpgraded, moveBUpgraded, uniteMoveUpgraded
        );
    }

    // Call this method when a move is learned in MovesController
    public void OnMoveLearnedFromController(MoveAsset moveAsset)
    {
        if (!IsOwner || playerManager?.MovesController == null) return;

        var currentStats = playerStats.Value;
        
        AvailableMoves newMoveA = currentStats.moveA;
        AvailableMoves newMoveB = currentStats.moveB;
        AvailableMoves newUniteMove = currentStats.uniteMove;
        bool newMoveAUpgraded = currentStats.moveAUpgraded;
        bool newMoveBUpgraded = currentStats.moveBUpgraded;
        bool newUniteMoveUpgraded = currentStats.uniteMoveUpgraded;

        // Update the appropriate move slot based on moveType
        switch (moveAsset.moveType)
        {
            case MoveType.MoveA:
                newMoveA = moveAsset.move;
                newMoveAUpgraded = moveAsset.isUpgraded;
                break;
            case MoveType.MoveB:
                newMoveB = moveAsset.move;
                newMoveBUpgraded = moveAsset.isUpgraded;
                break;
            case MoveType.UniteMove:
                newUniteMove = moveAsset.move;
                newUniteMoveUpgraded = moveAsset.isUpgraded;
                break;
        }

        UpdatePlayerStatsWithMoves(
            newMoveA, newMoveB, newUniteMove,
            currentStats.basicAttackName.ToString(),
            currentStats.battleItem,
            newMoveAUpgraded, newMoveBUpgraded, newUniteMoveUpgraded
        );
    }

    #endregion
}
