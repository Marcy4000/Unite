using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;

public enum PlayerState : byte
{
    Alive,
    Dead,
    Scoring
}

public class PlayerManager : NetworkBehaviour
{
    // TODO: reorganize these variable declarations

    private Pokemon pokemon;
    private MovesController movesController;
    private PlayerMovement playerMovement;
    private Aim aim;
    private PlayerControls playerControls;
    private AnimationManager animationManager;
    private Vision vision;
    private PassiveController passiveController;
    [SerializeField] private VisionController visionController;
    [SerializeField] private HPBar hpBar;

    [SerializeField] private PokemonBase selectedPokemon;

    private bool orangeTeam = false;
    private NetworkVariable<PlayerState> playerState = new NetworkVariable<PlayerState>(PlayerState.Alive);
    private ushort maxEnergyCarry;
    private NetworkVariable<ushort> currentEnergy = new NetworkVariable<ushort>();

    private BattleActionStatus scoreStatus = new BattleActionStatus(0);
    private bool isScoring = false;

    private bool isRecalling;
    private float recallTime;

    private PlayerStats playerStats;

    private string resourcePath = "Assets/Prefabs/Objects/Objects/AeosEnergy.prefab";

    private NetworkVariable<FixedString32Bytes> lobbyPlayerId = new NetworkVariable<FixedString32Bytes>(writePerm:NetworkVariableWritePermission.Owner);

    private Dictionary<StatusType, Action> statusAddedActions;
    private Dictionary<StatusType, Action> statusRemovedActions;

    private NetworkList<ScoreBoost> goalBuffs;
    private List<float> goalBuffsTimers = new List<float>();

    private GoalZone goalZone;

    public Pokemon Pokemon { get => pokemon; }
    public MovesController MovesController { get => movesController; }
    public Aim Aim { get => aim; }
    public PlayerMovement PlayerMovement { get => playerMovement; }
    public AnimationManager AnimationManager { get => animationManager; }
    public VisionController VisionController { get => visionController; }
    public Vision Vision { get => vision; }
    public HPBar HPBar { get => hpBar; }
    public PassiveController PassiveController { get => passiveController; }
    public PlayerControls PlayerControls { get => playerControls; }
    public bool IsScoring { get => isScoring; }

    public PlayerState PlayerState { get => playerState.Value; }

    public bool OrangeTeam { get => orangeTeam; }
    public ushort MaxEnergyCarry { get => maxEnergyCarry; set => maxEnergyCarry = value; }
    public ushort CurrentEnergy { get => currentEnergy.Value; }
    public BattleActionStatus ScoreStatus { get => scoreStatus; }

    public GoalZone GoalZone { get => goalZone; set => goalZone = value; }

    public PlayerStats PlayerStats { get => playerStats; set => playerStats = value; }

    public Player LobbyPlayer { get => LobbyController.Instance.GetPlayerByID(lobbyPlayerId.Value.ToString()); }

    private float maxScoreTime;

    public event Action<int> onGoalScored;
    public event Action OnRespawn;

    private Vector3 deathPosition = new Vector3(0, -50, 0);
    private Coroutine stopMovementCoroutine;

    private void Awake()
    {
        pokemon = GetComponent<Pokemon>();
        movesController = GetComponent<MovesController>();
        aim = GetComponent<Aim>();
        playerMovement = GetComponent<PlayerMovement>();
        animationManager = GetComponent<AnimationManager>();
        vision = GetComponent<Vision>();
        passiveController = GetComponent<PassiveController>();

        maxEnergyCarry = 30;
        scoreStatus.AddStatus(ActionStatusType.Disabled);
        if (IsServer)
        {
            currentEnergy.Value = 0;
        }

        pokemon.OnPokemonInitialized += OnPokemonInitialized;
        lobbyPlayerId.OnValueChanged += (previous, current) =>
        {
            hpBar.UpdatePlayerName(LobbyController.Instance.GetPlayerByID(current.ToString()).Data["PlayerName"].Value);
        };

        goalBuffs = new NetworkList<ScoreBoost>();

        InitializeStatusActions();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            lobbyPlayerId.Value = LobbyController.Instance.Player.Id;
            ChangeSelectedPokemonRpc(LobbyController.Instance.Player.Data["SelectedCharacter"].Value);
        }
    }

    public void Initialize()
    {
        bool currentTeam = LobbyController.Instance.GetLocalPlayerTeam();

        aim.TeamToIgnore = orangeTeam;
        visionController.TeamToIgnore = orangeTeam;
        vision.CurrentTeam = orangeTeam;
        vision.HasATeam = true;
        vision.IsVisible = true;

        hpBar.InitializeEnergyUI(PokemonType.Player, OrangeTeam, IsOwner);
        hpBar.UpdateHpBarColor(currentTeam != orangeTeam, IsOwner);

        if (IsOwner)
        {
            CameraController cameraController = FindObjectOfType<CameraController>();
            cameraController.Initialize(transform);

            playerControls = new PlayerControls();
            playerControls.asset.Enable();

            pokemon.OnLevelChange += OnPokemonLevelUp;
            pokemon.OnDeath += OnPokemonDeath;
            pokemon.OnDamageTaken += OnPokemonDamage;
            pokemon.OnStatusChange += OnPokemonStatusChange;
            playerState.OnValueChanged += OnPlayerStateChange;

            movesController.onBasicAttackPerformed += () => CancelRecall();
            movesController.onMovePerformed += (MoveBase) => CancelRecall();

            pokemon.OnKnockback += playerMovement.Knockback;
            pokemon.OnKnockup += playerMovement.Knockup;

            scoreStatus.OnStatusChange += () =>
            {
                bool showLock = scoreStatus.HasStatus(ActionStatusType.Busy) || scoreStatus.HasStatus(ActionStatusType.Stunned);
                if (showLock || scoreStatus.HasStatus(ActionStatusType.Disabled))
                {
                    EndScoring();
                }
                BattleUIManager.instance.SetEnergyBallLock(showLock);
            };

            visionController.IsEnabled = true;
            vision.SetVisibility(true);
        }
        else
        {
            visionController.IsEnabled = currentTeam == OrangeTeam;
        }

        vision.OnBushChanged += (bush) =>
        {
            visionController.CurrentBush = bush;
        };

        pokemon.OnEvolution += HandleEvolution;
        currentEnergy.OnValueChanged += OnEnergyAmountChange;

        if (!string.IsNullOrEmpty(lobbyPlayerId.Value.ToString()))
        {
            hpBar.UpdatePlayerName(LobbyController.Instance.GetPlayerByID(lobbyPlayerId.Value.ToString()).Data["PlayerName"].Value);
        }

        UpdateEnergyGraphic();
        AssignVisionObjects();
    }

    private void InitializeStatusActions()
    {
        statusAddedActions = new Dictionary<StatusType, Action>
        {
            { StatusType.Immobilized, () =>
                {
                    playerMovement.CanMove = false;
                    animationManager.SetBool("Walking", false);
                }
            },
            { StatusType.Frozen, () =>
                {
                    playerMovement.CanMove = false;
                    movesController.CancelAllMoves();
                    EndScoring();
                    movesController.AddMoveStatus(0, ActionStatusType.Stunned);
                    movesController.AddMoveStatus(1, ActionStatusType.Stunned);
                    movesController.AddMoveStatus(2, ActionStatusType.Stunned);
                    movesController.BasicAttackStatus.AddStatus(ActionStatusType.Stunned);
                    scoreStatus.AddStatus(ActionStatusType.Stunned);
                }
            },
            { StatusType.Incapacitated, ApplyStun },
            { StatusType.Asleep, ApplyStun },
            { StatusType.Bound, ApplyStun },
            { StatusType.VisionObscuring, () => VisionController.IsBlinded = true },
            { StatusType.Invisible, () => SetPlayerVisibilityRPC(false) }
            // Add other statuses
        };

        statusRemovedActions = new Dictionary<StatusType, Action>
        {
            { StatusType.Immobilized, () => playerMovement.CanMove = true },
            { StatusType.Frozen, RemoveStun },
            { StatusType.Incapacitated, RemoveStun },
            { StatusType.Asleep, RemoveStun },
            { StatusType.Bound, RemoveStun },
            { StatusType.VisionObscuring, () => VisionController.IsBlinded = false },
            { StatusType.Invisible, () => SetPlayerVisibilityRPC(true) }
            // Add other statuses
        };
    }

    private void OnEnergyAmountChange(ushort prev, ushort curr)
    {
        hpBar.UpdateEnergyAmount(curr);
        UpdateEnergyGraphic();
    }

    private void OnPokemonInitialized()
    {
        bool currentTeam = LobbyController.Instance.GetLocalPlayerTeam();
        AssignVisionObjects();
        vision.SetVisibility(currentTeam == OrangeTeam);

        passiveController.LearnPassive();
    }

    public void StopMovementForTime(float time, bool setTrigger=true)
    {
        if (stopMovementCoroutine != null)
        {
            StopCoroutine(stopMovementCoroutine);
        }
        stopMovementCoroutine = StartCoroutine(StopMovementForTimeCoroutine(time, setTrigger));
    }

    private IEnumerator StopMovementForTimeCoroutine(float time, bool setTrigger=true)
    {
        playerMovement.CanMove = false;
        yield return new WaitForSeconds(time);
        playerMovement.CanMove = true;

        if (setTrigger)
        {
            animationManager.SetTrigger("Transition");
        }
    }

    private void HandleEvolution()
    {
        pokemon.ActiveModel.GetComponentInChildren<Animator>().keepAnimatorStateOnDisable = true;
        animationManager.AssignAnimator(pokemon.ActiveModel.GetComponentInChildren<Animator>());
        AssignVisionObjects();
    }

    private void AssignVisionObjects()
    {
        vision.ResetObjects();
        vision.AddObject(pokemon.ActiveModel);
        vision.AddObject(hpBar.gameObject);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void ChangeSelectedPokemonRpc(string pokemonName)
    {
        PokemonBase newPokemon = CharactersList.Instance.GetCharacterFromString(pokemonName).pokemon;
        selectedPokemon = newPokemon;
        pokemon.SetNewPokemon(selectedPokemon);
        hpBar.SetPokemon(pokemon);
        HandleEvolution();
    }

    [Rpc(SendTo.Owner)]
    public void UpdatePosAndRotRPC(Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;
    }

    private void OnPlayerStateChange(PlayerState prev, PlayerState curr)
    {
        if (curr != PlayerState.Dead && transform.position.y == deathPosition.y)
        {
            short pos = NumberEncoder.FromBase64<short>(LobbyController.Instance.Player.Data["PlayerPos"].Value);
            Transform spawnpoint = OrangeTeam ? SpawnpointManager.Instance.GetOrangeTeamSpawnpoint(pos) : SpawnpointManager.Instance.GetBlueTeamSpawnpoint(pos);
            UpdatePosAndRotRPC(spawnpoint.position, spawnpoint.rotation);
            playerMovement.CanMove = true;
        }
    }

    private void OnPokemonDamage(DamageInfo info)
    {
        if (isScoring)
        {
            EndScoring();
        }
    }

    private void OnPokemonDeath(DamageInfo info)
    {
        SpawnEnergy(currentEnergy.Value);
        ResetEnergyRPC();
        GiveExpRpc(info.attackerId, transform.position);
        // TODO: make moves end on death while not breaking everything
        transform.DOKill();
        //StopAllCoroutines();
        playerMovement.CanMove = false;
        movesController.CancelAllMoves();

        CameraController.Instance.ForcePan(true);

        UpdatePosAndRotRPC(deathPosition, Quaternion.identity);
        ChangeCurrentState(PlayerState.Dead);
    }

    public void ChangeCurrentTeam(bool isOrange)
    {
        orangeTeam = isOrange;
    }

    public void Respawn()
    {
        CameraController.Instance.ForcePan(false);
        ChangeCurrentState(PlayerState.Alive);
        pokemon.HealDamage(pokemon.GetMaxHp());
        movesController.UnlockEveryAction();
        scoreStatus.RemoveStatus(ActionStatusType.Busy);
        NotifyRespawnRPC();
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyRespawnRPC()
    {
        OnRespawn?.Invoke();
    }

    [Rpc(SendTo.Server)]
    private void ChangeCurrentStateRpc(PlayerState newState)
    {
        playerState.Value = newState;
    }

    public void ChangeCurrentState(PlayerState newState)
    {
        if (IsServer)
        {
            playerState.Value = newState;
        }
        else
        {
            ChangeCurrentStateRpc(newState);
        }
    }

    private void Update()
    {
        if (IsServer)
        {
            for (int i = goalBuffs.Count; i > 0; i--)
            {
                int index = i - 1;
                ScoreBoost change = goalBuffs[index];
                if (change.IsTimed)
                {
                    goalBuffsTimers[index] -= Time.deltaTime;
                    if (goalBuffsTimers[index] <= 0)
                    {
                        goalBuffs.RemoveAt(index);
                        goalBuffsTimers.RemoveAt(index);
                    }
                }
            }
        }

        if (!IsOwner)
        {
            return;
        }

        if (playerState.Value != PlayerState.Alive)
        {
            return;
        }

        //HandlePokemonStatuses();

        if (playerControls.Movement.Score.WasPressedThisFrame())
        {
            StartScoring();
        }

        if (playerControls.Movement.Score.IsPressed())
        {
            HandleScoring();
        }

        if (playerControls.Movement.Score.WasReleasedThisFrame())
        {
            EndScoring();
        }

        if (playerControls.Movement.Recall.WasReleasedThisFrame())
        {
            StartRecalling();
        }

        if (isRecalling)
        {
            recallTime -= Time.deltaTime;
            BattleUIManager.instance.UpdateRecallBar(1f-recallTime / 3f);

            if (recallTime <= 0)
            {
                short pos = NumberEncoder.FromBase64<short>(LobbyController.Instance.Player.Data["PlayerPos"].Value);
                Transform spawnpoint = OrangeTeam ? SpawnpointManager.Instance.GetOrangeTeamSpawnpoint(pos) : SpawnpointManager.Instance.GetBlueTeamSpawnpoint(pos);
                UpdatePosAndRotRPC(spawnpoint.position, spawnpoint.rotation);
                isRecalling = false;
                BattleUIManager.instance.SetRecallBarActive(false);
            }

            if (playerMovement.IsMoving)
            {
                CancelRecall();
            }
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            GainEnergyRPC(5);
        }
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            LoseEnergyRPC(5);
        }
#endif
    }

    private void CancelRecall()
    {
        isRecalling = false;
        BattleUIManager.instance.SetRecallBarActive(false);
    }

    private void StartRecalling()
    {
        if (isRecalling)
        {
            return;
        }

        isRecalling = true;
        recallTime = 3f;
        BattleUIManager.instance.SetRecallBarActive(true);
    }

    private void StartScoring()
    {
        if (!scoreStatus.HasStatus(ActionStatusType.None) || currentEnergy.Value == 0)
        {
            return;
        }

        goalZone.GetAlliesInGoal(OrangeTeam, out int alliesInGoal, out int enemiesInGoal);

        isScoring = true;
        
        maxScoreTime = ScoringSystem.CalculateTrueScoreTime(alliesInGoal, GetScoreBuffs(), currentEnergy.Value);
        playerMovement.CanMove = false;
        scoreStatus.Cooldown = 0;
        BattleUIManager.instance.SetEnergyBallState(true);
    }

    private void HandleScoring()
    {
        if (!isScoring)
        {
            return;
        }

        scoreStatus.Cooldown += Time.deltaTime;
        BattleUIManager.instance.UpdateScoreGauge(scoreStatus.Cooldown, maxScoreTime);

        if (scoreStatus.Cooldown >= maxScoreTime)
        {
            ScorePoints();
        }
    }

    private void EndScoring()
    {
        if (!isScoring)
        {
            return;
        }

        isScoring = false;
        scoreStatus.Cooldown = 0;
        playerMovement.CanMove = true;
        BattleUIManager.instance.UpdateScoreGauge(scoreStatus.Cooldown, maxScoreTime);
        BattleUIManager.instance.SetEnergyBallState(false);
    }

    private void ScorePoints()
    {
        ScoreInfo score = new ScoreInfo(currentEnergy.Value, NetworkObjectId);
        GameManager.Instance.GoalScoredRpc(score);
        onGoalScored?.Invoke(currentEnergy.Value);
        GivePokemonExperience();
        movesController.IncrementUniteCharge(12000);
        ResetEnergyRPC();
        EndScoring();
    }

    private void GivePokemonExperience()
    {
        if (currentEnergy.Value == 2)
        {
            pokemon.GainExperience(50);
        }
        else if (currentEnergy.Value > 2)
        {
            pokemon.GainExperience(10 * currentEnergy.Value + 40);
        }
    }

    private void OnPokemonLevelUp()
    {
        switch (pokemon.CurrentLevel)
        {
            case 8:
                ChangeMaxEnergyRPC(40);
                break;
            case 11:
                ChangeMaxEnergyRPC(50);
                break;
        }
    }

    private void HandlePokemonStatuses()
    {
        if (pokemon.StatusEffects.Count == 0)
        {
            return;
        }

        foreach (StatusEffect effect in pokemon.StatusEffects)
        {
            switch (effect.Type)
            {
                case StatusType.Immobilized:
                    break;
                case StatusType.Incapacitated:
                    break;
                case StatusType.Asleep:
                    break;
                case StatusType.Frozen:
                    break;
                case StatusType.Bound:
                    break;
                case StatusType.Unstoppable:
                    break;
                case StatusType.Invincible:
                    break;
                case StatusType.Untargetable:
                    break;
                case StatusType.HindranceResistance:
                    break;
                case StatusType.Invisible:
                    break;
                case StatusType.VisionObscuring:
                    break;
            }
        }
    }

    private void ApplyStun()
    {
        playerMovement.CanMove = false;
        animationManager.SetTrigger("Stun");
        movesController.CancelAllMoves();
        EndScoring();
        CancelRecall();
        movesController.AddMoveStatus(0, ActionStatusType.Stunned);
        movesController.AddMoveStatus(1, ActionStatusType.Stunned);
        movesController.AddMoveStatus(2, ActionStatusType.Stunned);
        movesController.BasicAttackStatus.AddStatus(ActionStatusType.Stunned);
        scoreStatus.AddStatus(ActionStatusType.Stunned);
    }

    private void RemoveStun()
    {
        playerMovement.CanMove = true;
        animationManager.SetTrigger("Transition");
        movesController.RemoveMoveStatus(0, ActionStatusType.Stunned);
        movesController.RemoveMoveStatus(1, ActionStatusType.Stunned);
        movesController.RemoveMoveStatus(2, ActionStatusType.Stunned);
        movesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Stunned);
        scoreStatus.RemoveStatus(ActionStatusType.Stunned);
    }

    private void OnPokemonStatusChange(StatusEffect effect, bool added)
    {
        // This is so incredibly stupid but it'll do for now
        // Update: no longer as stupid, still stupid
        Debug.Log("Status changed");
        if (added && statusAddedActions.TryGetValue(effect.Type, out Action addAction))
        {
            addAction.Invoke();
        }
        else if (!added && statusRemovedActions.TryGetValue(effect.Type, out Action removeAction))
        {
            removeAction.Invoke();
        }
    }

    [Rpc(SendTo.Server)]
    public void GainEnergyRPC(ushort amount=1)
    {
        currentEnergy.Value = (ushort)Mathf.Clamp(currentEnergy.Value + amount, 0, maxEnergyCarry);
    }

    [Rpc(SendTo.Server)]
    public void LoseEnergyRPC(short amount=1)
    {
        currentEnergy.Value = (ushort)Mathf.Clamp(currentEnergy.Value - amount, 0, maxEnergyCarry);
    }

    [Rpc(SendTo.Server)]
    public void ResetEnergyRPC()
    {
        currentEnergy.Value = 0;
    }

    [Rpc(SendTo.Everyone)]
    public void ChangeMaxEnergyRPC(ushort amount)
    {
        maxEnergyCarry = amount;
        if (IsOwner)
        {
            UpdateEnergyGraphic();
        }
    }

    public bool IsEnergyFull()
    {
        return currentEnergy.Value == maxEnergyCarry;
    }

    public ushort AvailableEnergy()
    {
        return (ushort)(maxEnergyCarry - currentEnergy.Value);
    }

    public void UpdateEnergyGraphic()
    {
        if (IsOwner)
        {
            BattleUIManager.instance.UpdateEnergyUI(currentEnergy.Value, maxEnergyCarry);
        }
    }

    private void SpawnEnergy(ushort amount)
    {
        int numFives = amount / 5;
        int remainderOnes = amount % 5;

        for (int i = 0; i < numFives; i++)
        {
            SpawnEnergyRpc(true);
        }

        for (int i = 0; i < remainderOnes; i++)
        {
            SpawnEnergyRpc(false);
        }
    }

    [Rpc(SendTo.Server)]
    private void SpawnEnergyRpc(bool isBig)
    {
        Vector3 offset = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f));
        Addressables.LoadAssetAsync<GameObject>(resourcePath).Completed += (handle) =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                GameObject prefab = handle.Result;
                GameObject spawnedObject = Instantiate(prefab, transform.position + offset, Quaternion.identity);
                spawnedObject.GetComponent<AeosEnergy>().LocalBigEnergy = isBig;
                spawnedObject.GetComponent<NetworkObject>().Spawn(true);
            }
            else
            {
                Debug.LogError($"Failed to load addressable: {handle.OperationException}");
            }
        };
    }

    [Rpc(SendTo.Server)]
    public void AddScoreBoostRPC(ScoreBoost change)
    {
        goalBuffs.Add(change);
        if (change.IsTimed)
        {
            goalBuffsTimers.Add(change.Duration);
        }
        else
        {
            goalBuffsTimers.Add(-1);
        }
    }

    [Rpc(SendTo.Server)]
    public void RemoveScoreBoostWithIDRPC(ushort id)
    {
        for (int i = 0; i < goalBuffs.Count; i++)
        {
            if (goalBuffs[i].ID == id)
            {
                goalBuffs.RemoveAt(i);
                goalBuffsTimers.RemoveAt(i);
                return;
            }
        }
    }

    public List<ScoreBoost> GetScoreBuffs()
    {
        List<ScoreBoost> buffs = new List<ScoreBoost>();
        foreach (ScoreBoost boost in goalBuffs)
        {
            buffs.Add(boost);
        }
        return buffs;
    }

    [Rpc(SendTo.Everyone)]
    public void SetPlayerVisibilityRPC(bool isVisible)
    {
        vision.IsVisible = isVisible;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void GiveExpRpc(ulong attackerID, Vector3 deathPos)
    {
        Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[attackerID].GetComponent<Pokemon>();

        float baseExp = GetBaseExp();

        Dictionary<int, (float koerExp, float proximityExp)> expDistribution = new Dictionary<int, (float, float)>
        {
            {1, (1.0f, 0.0f)},
            {2, (1.0f, 0.5f)},
            {3, (1.0f, 0.25f)},
            {4, (1.0f, 0.1667f)},
            {5, (1.0f, 0.125f)}
        };

        List<Pokemon> playersInProximity;

        if (attacker.TryGetComponent(out PlayerManager player))
        {
            playersInProximity = FindPlayersInProximity(deathPos, 6f, player.OrangeTeam);
        }
        else
        {
            return;
        }

        DistributeExperience(attacker, playersInProximity, baseExp, expDistribution);
    }

    private List<Pokemon> FindPlayersInProximity(Vector3 koPosition, float range, bool orangeTeam)
    {
        List<Pokemon> playersInProximity = new List<Pokemon>();

        foreach (var playerObject in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
        {
            PlayerManager player = playerObject.GetComponent<PlayerManager>();
            if (player != null && player.OrangeTeam == orangeTeam && Vector3.Distance(player.transform.position, koPosition) <= range)
            {
                playersInProximity.Add(player.Pokemon);
            }
        }

        return playersInProximity;
    }

    private void DistributeExperience(Pokemon attacker, List<Pokemon> playersInProximity, float baseExp, Dictionary<int, (float koerExp, float proximityExp)> expDistribution)
    {
        int playersCount = playersInProximity.Count;
        if (!expDistribution.TryGetValue(playersCount, out var expValues))
        {
            expValues = expDistribution[1];
        }

        float attackerExp = baseExp * expValues.koerExp;
        attacker.GainExperience(Mathf.FloorToInt(attackerExp));

        float proximityExp = baseExp * expValues.proximityExp;
        foreach (var player in playersInProximity)
        {
            if (player != attacker)
            {
                player.GainExperience(Mathf.FloorToInt(proximityExp));
            }
        }
    }

    private float GetBaseExp()
    {
        switch (pokemon.CurrentLevel)
        {
            case 0:
                return 20;
            case 1:
                return 60;
            case 2:
                return 100;
            case 3:
                return 140;
            case 4:
                return 180;
            case 5:
                return 220;
            case 6:
                return 260;
            case 7:
                return 300;
            case 8:
                return 360;
            case 9:
                return 420;
            case 10:
                return 480;
            case 11:
                return 540;
            case 12:
                return 600;
            case 13:
                return 700;
            case 14:
                return 800;
            default:
                return 0;
        }
    }

}
