using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerManager : MonoBehaviour
{
    private Pokemon pokemon;
    private MovesController movesController;
    private PlayerMovement playerMovement;
    private Aim aim;
    private PlayerControls playerControls;

    private bool orangeTeam;
    private int maxEnergyCarry;
    private int currentEnergy;

    private bool canScore;
    private bool isScoring = false;

    public Pokemon Pokemon { get => pokemon; }
    public MovesController MovesController { get => movesController; }
    public Aim Aim { get => aim; }
    public bool IsScoring { get => isScoring; }

    public bool OrangeTeam { get => orangeTeam; set => orangeTeam = value; }
    public int MaxEnergyCarry { get => maxEnergyCarry; set => maxEnergyCarry = value; }
    public int CurrentEnergy { get => currentEnergy; set => currentEnergy = value; }
    public bool CanScore { get => canScore; set => canScore = value; }

    private float scoreTimer, maxScoreTime;

    public event Action<int> onGoalScored;

    private void Awake()
    {
        pokemon = GetComponent<Pokemon>();
        movesController = GetComponent<MovesController>();
        aim = GetComponent<Aim>();
        playerMovement = GetComponent<PlayerMovement>();

        playerControls = new PlayerControls();
        playerControls.asset.Enable();

        maxEnergyCarry = 30;

        pokemon.OnLevelChange += OnPokemonLevelUp;
    }

    private void Start()
    {
        UpdateEnergyGraphic();
    }

    private void Update()
    {
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
        isScoring = false;
        scoreTimer = 0;
        playerMovement.CanMove = true;
        BattleUIManager.instance.UpdateScoreGauge(scoreTimer, maxScoreTime);
        BattleUIManager.instance.SetEnergyBallState(false);
    }

    private void ScorePoints()
    {
        GameManager.instance.GoalScored(orangeTeam, currentEnergy);
        onGoalScored?.Invoke(currentEnergy);
        GivePokemonExperience();
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
        switch (pokemon.CurrentLevel)
        {
            case 8:
                ChangeMaxEnergy(40);
                break;
            case 12:
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
        BattleUIManager.instance.UpdateEnergyUI(currentEnergy, maxEnergyCarry);
    }
}
