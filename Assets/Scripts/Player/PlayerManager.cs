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

public enum PlayerState
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
    private short maxEnergyCarry;
    private NetworkVariable<short> currentEnergy = new NetworkVariable<short>();

    private BattleActionStatus scoreStatus = new BattleActionStatus(0);
    private bool isScoring = false;

    private string resourcePath = "Assets/Prefabs/Objects/Objects/AeosEnergy.prefab";

    private NetworkVariable<FixedString32Bytes> lobbyPlayerId = new NetworkVariable<FixedString32Bytes>(writePerm:NetworkVariableWritePermission.Owner);

    private Dictionary<StatusType, Action> statusAddedActions;
    private Dictionary<StatusType, Action> statusRemovedActions;

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
    public short MaxEnergyCarry { get => maxEnergyCarry; set => maxEnergyCarry = value; }
    public short CurrentEnergy { get => currentEnergy.Value; }
    public BattleActionStatus ScoreStatus { get => scoreStatus; }

    public Player LobbyPlayer { get => LobbyController.Instance.GetPlayerByID(lobbyPlayerId.Value.ToString()); }

    private float maxScoreTime;

    public event Action<int> onGoalScored;

    private Vector3 deathPosition = new Vector3(0, -20, 0);
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
        bool currentTeam = LobbyController.Instance.Player.Data["PlayerTeam"].Value == "Orange";

        aim.TeamToIgnore = orangeTeam;
        visionController.TeamToIgnore = orangeTeam;
        vision.CurrentTeam = orangeTeam;
        vision.HasATeam = true;
        vision.IsVisible = true;

        hpBar.InitializeEnergyUI(PokemonType.Player, OrangeTeam, IsOwner);

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

            scoreStatus.OnStatusChange += () =>
            {
                bool showLock = scoreStatus.HasStatus(ActionStatusType.Busy) || scoreStatus.HasStatus(ActionStatusType.Stunned);
                BattleUIManager.instance.SetEnergyBallLock(showLock);
            };

            visionController.IsEnabled = true;
            vision.SetVisibility(true);
        }
        else
        {
            visionController.IsEnabled = currentTeam == OrangeTeam;
        }

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
                    scoreStatus.AddStatus(ActionStatusType.Stunned);
                }
            },
            { StatusType.Incapacitated, ApplyStun },
            { StatusType.Asleep, ApplyStun },
            { StatusType.Bound, ApplyStun },
            { StatusType.VisionObscuring, () => VisionController.IsBlinded = true }
            // Add other statuses
        };

        statusRemovedActions = new Dictionary<StatusType, Action>
        {
            { StatusType.Immobilized, () => playerMovement.CanMove = true },
            { StatusType.Frozen, RemoveStun },
            { StatusType.Incapacitated, RemoveStun },
            { StatusType.Asleep, RemoveStun },
            { StatusType.Bound, RemoveStun },
            { StatusType.VisionObscuring, () => VisionController.IsBlinded = false }
            // Add other statuses
        };
    }

    private void OnEnergyAmountChange(short prev, short curr)
    {
        hpBar.UpdateEnergyAmount(curr);
        UpdateEnergyGraphic();
    }

    private void OnPokemonInitialized()
    {
        bool currentTeam = LobbyController.Instance.Player.Data["PlayerTeam"].Value == "Orange";
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
        PokemonBase newPokemon = CharactersList.instance.GetCharacterFromString(pokemonName).pokemon;
        selectedPokemon = newPokemon;
        pokemon.SetNewPokemon(selectedPokemon);
        hpBar.SetPokemon(pokemon);
        HandleEvolution();
    }

    [Rpc(SendTo.Owner)]
    public void UpdatePosAndRotRPC(Vector3 pos, Quaternion rot)
    {
        playerMovement.CharacterController.enabled = false;
        transform.position = pos;
        transform.rotation = rot;
        playerMovement.CharacterController.enabled = true;
    }

    private void OnPlayerStateChange(PlayerState prev, PlayerState curr)
    {
        if (curr != PlayerState.Dead && transform.position.y == deathPosition.y)
        {
            Transform spawnpoint = OrangeTeam ? SpawnpointManager.Instance.GetOrangeTeamSpawnpoint() : SpawnpointManager.Instance.GetBlueTeamSpawnpoint();
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
        playerMovement.CanMove = false;
        UpdatePosAndRotRPC(deathPosition, Quaternion.identity);
        ChangeCurrentState(PlayerState.Dead);
    }

    public void ChangeCurrentTeam(bool isOrange)
    {
        orangeTeam = isOrange;
    }

    public void Respawn()
    {
        ChangeCurrentState(PlayerState.Alive);
        pokemon.HealDamage(pokemon.GetMaxHp());
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

        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            GainEnergyRPC(5);
        }
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            LoseEnergyRPC(5);
        }
    }

    private void StartScoring()
    {
        if (!scoreStatus.HasStatus(ActionStatusType.None) || currentEnergy.Value == 0)
        {
            return;
        }

        isScoring = true;
        maxScoreTime = ScoringSystem.CalculateApproximateScoreTime(currentEnergy.Value);
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
                ChangeMaxEnergy(40);
                break;
            case 11:
                ChangeMaxEnergy(50);
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
        movesController.AddMoveStatus(0, ActionStatusType.Stunned);
        movesController.AddMoveStatus(1, ActionStatusType.Stunned);
        movesController.AddMoveStatus(2, ActionStatusType.Stunned);
        scoreStatus.AddStatus(ActionStatusType.Stunned);
    }

    private void RemoveStun()
    {
        playerMovement.CanMove = true;
        animationManager.SetTrigger("Transition");
        movesController.RemoveMoveStatus(0, ActionStatusType.Stunned);
        movesController.RemoveMoveStatus(1, ActionStatusType.Stunned);
        movesController.RemoveMoveStatus(2, ActionStatusType.Stunned);
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
    public void GainEnergyRPC(short amount=1)
    {
        currentEnergy.Value = (short)Mathf.Clamp(currentEnergy.Value + amount, 0, maxEnergyCarry);
    }

    [Rpc(SendTo.Server)]
    public void LoseEnergyRPC(short amount=1)
    {
        currentEnergy.Value = (short)Mathf.Clamp(currentEnergy.Value - amount, 0, maxEnergyCarry);
    }

    [Rpc(SendTo.Server)]
    public void ResetEnergyRPC()
    {
        currentEnergy.Value = 0;
    }

    public void ChangeMaxEnergy(short amount)
    {
        maxEnergyCarry = amount;
        UpdateEnergyGraphic();
    }

    public bool IsEnergyFull()
    {
        return currentEnergy.Value == maxEnergyCarry;
    }

    public short AvailableEnergy()
    {
        return (short)(maxEnergyCarry - currentEnergy.Value);
    }

    public void UpdateEnergyGraphic()
    {
        if (IsOwner)
        {
            BattleUIManager.instance.UpdateEnergyUI(currentEnergy.Value, maxEnergyCarry);
        }
    }

    private void SpawnEnergy(short amount)
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
}
