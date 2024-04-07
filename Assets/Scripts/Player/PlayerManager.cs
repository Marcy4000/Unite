using Cinemachine;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerState
{
    Alive,
    Dead,
    Scoring
}

public class PlayerManager : NetworkBehaviour
{
    private Pokemon pokemon;
    private MovesController movesController;
    private PlayerMovement playerMovement;
    private Aim aim;
    private PlayerControls playerControls;
    private AnimationManager animationManager;
    [SerializeField] private HPBar hpBar;

    [SerializeField] private PokemonBase selectedPokemon;

    private NetworkVariable<bool> orangeTeam = new NetworkVariable<bool>();
    private NetworkVariable<PlayerState> playerState = new NetworkVariable<PlayerState>(PlayerState.Alive);
    private int maxEnergyCarry;
    private int currentEnergy;

    private bool canScore;
    private bool isScoring = false;

    public Pokemon Pokemon { get => pokemon; }
    public MovesController MovesController { get => movesController; }
    public Aim Aim { get => aim; }
    public PlayerMovement PlayerMovement { get => playerMovement; }
    public bool IsScoring { get => isScoring; }

    public PlayerState PlayerState { get => playerState.Value; }

    public bool OrangeTeam { get => orangeTeam.Value; }
    public int MaxEnergyCarry { get => maxEnergyCarry; set => maxEnergyCarry = value; }
    public int CurrentEnergy { get => currentEnergy; set => currentEnergy = value; }
    public bool CanScore { get => canScore; set => canScore = value; }

    private float scoreTimer, maxScoreTime;

    public event Action<int> onGoalScored;

    private Vector3 deathPosition = new Vector3(0, -20, 0);

    private void Awake()
    {
        pokemon = GetComponent<Pokemon>();
        movesController = GetComponent<MovesController>();
        aim = GetComponent<Aim>();
        playerMovement = GetComponent<PlayerMovement>();
        animationManager = GetComponent<AnimationManager>();

        maxEnergyCarry = 30;
        canScore = false;
        currentEnergy = 0;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            ChangeSelectedPokemonRpc(LobbyController.instance.Player.Data["SelectedCharacter"].Value);
        }
        orangeTeam.OnValueChanged += (previous, current) =>
        {
            aim.TeamToIgnore = current;
        };
        if (IsOwner)
        {
            CinemachineVirtualCamera virtualCamera = FindObjectOfType<CinemachineVirtualCamera>();
            virtualCamera.Follow = transform;
            virtualCamera.LookAt = transform;
            playerControls = new PlayerControls();
            playerControls.asset.Enable();
            pokemon.OnLevelChange += OnPokemonLevelUp;
            pokemon.OnDeath += OnPokemonDeath;
            pokemon.OnDamageTaken += OnPokemonDamage;
        }
        pokemon.OnEvolution += HandleEvolution;
        UpdateEnergyGraphic();
    }

    private void HandleEvolution()
    {
        animationManager.AssignAnimator(pokemon.ActiveModel.GetComponentInChildren<Animator>());
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

    private void OnPokemonDamage(DamageInfo info)
    {
        if (isScoring)
        {
            EndScoring();
        }
    }

    private void OnPokemonDeath(DamageInfo info)
    {
        transform.position = deathPosition;
        playerMovement.CanMove = false;
        ChangeCurrentState(PlayerState.Dead);
    }

    public void ChangeCurrentTeam(bool isOrange)
    {
        if (IsServer) {
            orangeTeam.Value = isOrange;
        } else {
            ChangeCurrentTeamRpc(isOrange);
        }
    }

    public void Respawn()
    {
        ChangeCurrentState(PlayerState.Alive);
        pokemon.HealDamage(pokemon.GetMaxHp());
        playerMovement.CanMove = true;
        Transform spawnpoint = OrangeTeam ? SpawnpointManager.Instance.GetOrangeTeamSpawnpoint() : SpawnpointManager.Instance.GetBlueTeamSpawnpoint();
        transform.position = new Vector3(spawnpoint.position.x, spawnpoint.position.y, spawnpoint.position.z);
        transform.rotation = spawnpoint.rotation;
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

    [Rpc(SendTo.Server)]
    private void ChangeCurrentTeamRpc(bool isOrange)
    {
        orangeTeam.Value = isOrange;
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

        if (playerState.Value != PlayerState.Dead && transform.position.y == deathPosition.y)
        {
            Transform spawnpoint = OrangeTeam ? SpawnpointManager.Instance.GetOrangeTeamSpawnpoint() : SpawnpointManager.Instance.GetBlueTeamSpawnpoint();
            transform.position = new Vector3(spawnpoint.position.x, spawnpoint.position.y, spawnpoint.position.z);
            transform.rotation = spawnpoint.rotation;
        }

        if (playerControls.Movement.Score.WasPressedThisFrame())
        {
            StartScoring();
        }

        if (playerControls.Movement.Score.IsPressed())
        {
            HandleScoring();
        }

        if (playerControls.Movement.Score.WasReleasedThisFrame() && CanScore)
        {
            EndScoring();
        }

        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            GainEnergy(5);
        }
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            LoseEnergy(5);
        }
    }

    private void StartScoring()
    {
        if (!canScore || currentEnergy == 0)
        {
            return;
        }

        isScoring = true;
        maxScoreTime = ScoringSystem.CalculateApproximateScoreTime(currentEnergy);
        playerMovement.CanMove = false;
        scoreTimer = 0;
        BattleUIManager.instance.SetEnergyBallState(true);
    }

    private void HandleScoring()
    {
        if (!isScoring)
        {
            return;
        }

        scoreTimer += Time.deltaTime;
        BattleUIManager.instance.UpdateScoreGauge(scoreTimer, maxScoreTime);

        if (scoreTimer >= maxScoreTime)
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
        scoreTimer = 0;
        playerMovement.CanMove = true;
        BattleUIManager.instance.UpdateScoreGauge(scoreTimer, maxScoreTime);
        BattleUIManager.instance.SetEnergyBallState(false);
    }

    private void ScorePoints()
    {
        GameManager.instance.GoalScoredRpc(OrangeTeam, currentEnergy);
        onGoalScored?.Invoke(currentEnergy);
        GivePokemonExperience();
        movesController.IncrementUniteCharge(12000);
        ResetEnergy();
        EndScoring();
    }

    private void GivePokemonExperience()
    {
        if (currentEnergy == 2)
        {
            pokemon.GainExperience(50);
        }
        else if (currentEnergy > 2)
        {
            pokemon.GainExperience(10 * currentEnergy + 40);
        }
    }

    private void OnPokemonLevelUp()
    {
        switch (pokemon.CurrentLevel.Value)
        {
            case 8:
                ChangeMaxEnergy(40);
                break;
            case 11:
                ChangeMaxEnergy(50);
                break;
        }
    }

    public void GainEnergy(int amount=1)
    {
        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0, maxEnergyCarry);
        UpdateEnergyGraphic();
    } 

    public void LoseEnergy(int amount=1)
    {
        currentEnergy = Mathf.Clamp(currentEnergy - amount, 0, maxEnergyCarry);
        UpdateEnergyGraphic();
    }

    public void ResetEnergy()
    {
        currentEnergy = 0;
        UpdateEnergyGraphic();
    }

    public void ChangeMaxEnergy(int amount)
    {
        maxEnergyCarry = amount;
        UpdateEnergyGraphic();
    }

    public void UpdateEnergyGraphic()
    {
        if (IsOwner)
        {
            BattleUIManager.instance.UpdateEnergyUI(currentEnergy, maxEnergyCarry);
        }
    }
}
