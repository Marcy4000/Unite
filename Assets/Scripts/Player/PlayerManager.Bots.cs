using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerManager
{
    private NetworkVariable<bool> botControlled = new NetworkVariable<bool>();
    private NetworkVariable<Team> botTeam = new NetworkVariable<Team>();
    private NetworkVariable<short> botCharacter = new NetworkVariable<short>(-1);
    private NetworkVariable<PlayerStats> botStats = new NetworkVariable<PlayerStats>();
    private Vector2 botMovement;
    private bool botReady;
    private bool botMatchStarted;
    private NetworkVariable<double> botRespawnAt = new NetworkVariable<double>();
    private int botKillsSinceDeath;
    private int botPointsSinceDeath;

    public bool IsBot => botControlled.Value;
    public bool IsLocalHuman => IsOwner && !IsBot;
    public bool BotReady => botReady;
    public BotLoadout BotLoadout => IsBot && LobbyController.Instance != null ? LobbyController.Instance.BotRoster?.GetBotLoadout(LobbyPlayer.Id) : null;
    public bool BotIsRecalling => isRecalling;
    public void TryBotRecall() { if (IsBot && IsServer && !playerMovement.IsMoving) StartRecalling(); }
    public void CancelBotRecall() { if (IsBot && IsServer) CancelRecall(); }
    public Vector2 MovementInput => IsBot ? botMovement : InputManager.Instance.Controls.Movement.Move.ReadValue<Vector2>();

    public void ConfigureBotBeforeSpawn(string id, Team team, short character)
    {
        if (IsSpawned) throw new System.InvalidOperationException("Configure a bot before spawning it.");
        botControlled.Value = true;
        lobbyPlayerId.Value = new FixedString32Bytes(id);
        botTeam.Value = team;
        botCharacter.Value = character;
        botStats.Value = new PlayerStats(id, 0, 0, 0, 0, 0, 0, 0);
    }

    private IEnumerator InitializeBotAvatar()
    {
        // Start initializes the normal move statuses. Cached Addressables loads
        // can complete synchronously, so never race them against Start.
        yield return null;
        if (!IsSpawned) yield break;
        ChangeCurrentTeam(botTeam.Value);
        Initialize();
        LoadSelectedPokemon(botCharacter.Value);
        float deadline = Time.realtimeSinceStartup + 60f;
        while (IsSpawned && pokemon.BaseStats == null && Time.realtimeSinceStartup < deadline) yield return null;
        if (!IsSpawned) yield break;
        if (pokemon.BaseStats == null)
        {
            LobbyController.Instance.ReportSessionFailure(Unite.Multiplayer.SessionFailureReason.ContentLoadFailed);
            yield break;
        }
        GameManager.Instance.AddPlayer(this);
        MinimapManager.Instance.CreatePlayerIcon(this);
        playerMovement.CanMove = false;
        botStats.OnValueChanged += OnBotStatsChanged;
        PlayerStats = botStats.Value;
        if (IsServer)
        {
            pokemon.OnDeath += RecordBotDeath;
            pokemon.OnOtherPokemonKilled += RecordBotKill;
            pokemon.OnDamageTaken += RecordBotDamageTaken;
            pokemon.OnDamageDealt += RecordBotDamageDealt;
            movesController.OnMoveLearned += RecordBotMove;
            onGoalScored += RecordBotScore;
            gameObject.AddComponent<BotBrain>();
        }
        botReady = true;
    }

    private void OnBotStatsChanged(PlayerStats before, PlayerStats after) => PlayerStats = after;

    public void SetBotMovement(Vector2 direction)
    {
        if (IsBot && IsServer) botMovement = Vector2.ClampMagnitude(direction, 1);
    }

    public void TickBotLifecycle()
    {
        if (!IsBot || !IsServer || !botReady || GameManager.Instance == null) return;
        var state = GameManager.Instance.GameState;
        if (state != GameState.Playing)
        {
            botMovement = Vector2.zero;
            playerMovement.CanMove = false;
            return;
        }
        if (!botMatchStarted)
        {
            botMatchStarted = true;
            playerMovement.CanMove = true;
            movesController.GameStarted();
        }
        if (PlayerState == PlayerState.Dead && NetworkManager.ServerTime.Time >= botRespawnAt.Value) Respawn();
    }

    public void TickBotScoring(bool requested)
    {
        if (!IsBot || !IsServer || PlayerState != PlayerState.Alive) return;
        if (!requested) { EndScoring(); return; }
        if (!isScoring) StartScoring();
        HandleScoring();
    }

    private void RecordBotDeath(DamageInfo info)
    {
        var stats = botStats.Value;
        stats.deaths++;
        botStats.Value = stats;
        botRespawnAt.Value = NetworkManager.ServerTime.Time + RespawnSystem.CalculateRespawnTime(pokemon.CurrentLevel, botKillsSinceDeath,
            botPointsSinceDeath, GameManager.Instance.MAX_GAME_TIME - GameManager.Instance.GameTime);
        botKillsSinceDeath = botPointsSinceDeath = 0;
        botMovement = Vector2.zero;
        PresentBotKillRpc(info);
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PresentBotKillRpc(DamageInfo info) => BattleUIManager.instance.ShowKill(info, pokemon, null);

    private void RecordBotKill(ulong id)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var target) ||
            target.GetComponent<Pokemon>()?.Type != PokemonType.Player) return;
        var stats = botStats.Value;
        stats.kills++;
        botStats.Value = stats;
        botKillsSinceDeath++;
    }

    private void RecordBotScore(int points)
    {
        var stats = botStats.Value;
        stats.score += (ushort)points;
        botStats.Value = stats;
        botPointsSinceDeath += points;
    }

    private void RecordBotDamageTaken(DamageInfo info)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(info.attackerId, out var source) ||
            source.GetComponent<Pokemon>() is not Pokemon attacker || attacker.Type != PokemonType.Player) return;
        var stats = botStats.Value;
        stats.damageTaken += (uint)Mathf.Max(0, pokemon.CalculateDamage(info, attacker));
        botStats.Value = stats;
    }

    private void RecordBotDamageDealt(ulong id, DamageInfo info)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var target) ||
            target.GetComponent<Pokemon>() is not Pokemon victim || victim.Type != PokemonType.Player) return;
        var stats = botStats.Value;
        stats.damageDealt += (uint)Mathf.Max(0, victim.CalculateDamage(info, pokemon));
        botStats.Value = stats;
    }

    private void RecordBotMove(MoveAsset move)
    {
        var stats = botStats.Value;
        stats.basicAttackName = pokemon.BaseStats.PokemonName;
        if (move.moveType == MoveType.MoveA) { stats.moveA = move.move; stats.moveAUpgraded = move.isUpgraded; }
        if (move.moveType == MoveType.MoveB) { stats.moveB = move.move; stats.moveBUpgraded = move.isUpgraded; }
        if (move.moveType == MoveType.UniteMove) { stats.uniteMove = move.move; stats.uniteMoveUpgraded = move.isUpgraded; }
        botStats.Value = stats;
    }
}
