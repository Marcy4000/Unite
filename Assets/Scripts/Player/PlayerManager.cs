using Cinemachine;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
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
    private Vision vision;
    [SerializeField] private VisionController visionController;
    [SerializeField] private HPBar hpBar;

    [SerializeField] private PokemonBase selectedPokemon;

    private NetworkVariable<bool> orangeTeam = new NetworkVariable<bool>();
    private NetworkVariable<PlayerState> playerState = new NetworkVariable<PlayerState>(PlayerState.Alive);
    private short maxEnergyCarry;
    private NetworkVariable<short> currentEnergy = new NetworkVariable<short>();

    private bool canScore;
    private bool isScoring = false;

    private string resourcePath = "Objects/AeosEnergy";

    private NetworkVariable<FixedString32Bytes> lobbyPlayerId = new NetworkVariable<FixedString32Bytes>(writePerm:NetworkVariableWritePermission.Owner);

    public Pokemon Pokemon { get => pokemon; }
    public MovesController MovesController { get => movesController; }
    public Aim Aim { get => aim; }
    public PlayerMovement PlayerMovement { get => playerMovement; }
    public AnimationManager AnimationManager { get => animationManager; }
    public VisionController VisionController { get => visionController; }
    public Vision Vision { get => vision; }
    public bool IsScoring { get => isScoring; }

    public PlayerState PlayerState { get => playerState.Value; }

    public bool OrangeTeam { get => orangeTeam.Value; }
    public short MaxEnergyCarry { get => maxEnergyCarry; set => maxEnergyCarry = value; }
    public short CurrentEnergy { get => currentEnergy.Value; }
    public bool CanScore { get => canScore; set => canScore = value; }

    public Player LobbyPlayer { get => LobbyController.Instance.GetPlayerByID(lobbyPlayerId.Value.ToString()); }

    private float scoreTimer, maxScoreTime;

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

        lobbyPlayerId.OnValueChanged += (previous, current) =>
        {
            hpBar.UpdatePlayerName(LobbyController.Instance.GetPlayerByID(current.ToString()).Data["PlayerName"].Value);
        };

        maxEnergyCarry = 30;
        canScore = false;
        if (IsServer)
        {
            currentEnergy.Value = 0;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            lobbyPlayerId.Value = LobbyController.Instance.Player.Id;
            ChangeSelectedPokemonRpc(LobbyController.Instance.Player.Data["SelectedCharacter"].Value);
        }

        bool currentTeam = LobbyController.Instance.Player.Data["PlayerTeam"].Value == "Orange";

        orangeTeam.OnValueChanged += (previous, current) =>
        {
            aim.TeamToIgnore = current;
            visionController.TeamToIgnore = current;
            vision.CurrentTeam = current;
            vision.HasATeam = true;
            vision.IsVisible = true;
            hpBar.InitializeEnergyUI(PokemonType.Player, current, IsOwner);
            if (IsOwner)
            {
                visionController.IsEnabled = true;
                vision.SetVisibility(true);
            }
            else
            {
                visionController.IsEnabled = currentTeam == current;
                vision.SetVisibility(currentTeam == current);
            }
        };

        hpBar.InitializeEnergyUI(PokemonType.Player, OrangeTeam, IsOwner);
        vision.HasATeam = true;
        vision.IsVisible = true;

        if (IsOwner)
        {
            CameraController cameraController = FindObjectOfType<CameraController>();
            cameraController.Initialize(transform);
            playerControls = new PlayerControls();
            playerControls.asset.Enable();
            pokemon.OnLevelChange += OnPokemonLevelUp;
            pokemon.OnDeath += OnPokemonDeath;
            pokemon.OnDamageTaken += OnPokemonDamage;
            visionController.IsEnabled = true;
        }
        else
        {
            visionController.IsEnabled = currentTeam == OrangeTeam;
        }


        NetworkObject.DestroyWithScene = true;

        pokemon.OnEvolution += HandleEvolution;
        currentEnergy.OnValueChanged += OnEnergyAmountChange;

        UpdateEnergyGraphic();
        AssignVisionObjects();
        vision.SetVisibility(currentTeam == OrangeTeam);
    }

    private void OnEnergyAmountChange(short prev, short curr)
    {
        hpBar.UpdateEnergyAmount(curr);
        UpdateEnergyGraphic();
    }

    public void StopMovementForTime(float time)
    {
        if (stopMovementCoroutine != null)
        {
            StopCoroutine(stopMovementCoroutine);
        }
        stopMovementCoroutine = StartCoroutine(StopMovementForTimeCoroutine(time));
    }

    private IEnumerator StopMovementForTimeCoroutine(float time)
    {
        playerMovement.CanMove = false;
        yield return new WaitForSeconds(time);
        playerMovement.CanMove = true;
        animationManager.SetTrigger("Transition");
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
        if (LobbyPlayer.Data["PlayerName"].Value != null)
        {
            hpBar.UpdatePlayerName(LobbyPlayer.Data["PlayerName"].Value);
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
            GainEnergyRPC(5);
        }
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            LoseEnergyRPC(5);
        }
    }

    private void StartScoring()
    {
        if (!canScore || currentEnergy.Value == 0)
        {
            return;
        }

        isScoring = true;
        maxScoreTime = ScoringSystem.CalculateApproximateScoreTime(currentEnergy.Value);
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
        GameObject energy = Instantiate(Resources.Load(resourcePath, typeof(GameObject)), transform.position + offset, Quaternion.identity) as GameObject;
        energy.GetComponent<AeosEnergy>().LocalBigEnergy = isBig;
        energy.GetComponent<NetworkObject>().Spawn();
    }
}
