using System.Collections;
using UnityEngine;

public class BattleUIManager : MonoBehaviour
{
    public static BattleUIManager instance;

    [SerializeField] private MoveUI[] moveUIs;
    [SerializeField] private UniteMoveUI uniteMoveUI;
    [SerializeField] private MoveLearnPanel moveLearnPanel;
    [SerializeField] private BattleItemUI battleItemUI;
    [SerializeField] private EnergyUI energyUI;
    [SerializeField] private ScoreUI blueScoreUI, orangeScoreUI;
    [SerializeField] private DeathScreenUI deathScreenUI;
    [SerializeField] private KillNotificationUI killNotificationUI;
    [SerializeField] private GoalStateUI goalStateUI;
    [SerializeField] private RecallBarUI recallBarUI;
    [SerializeField] private ScoreboardUI scoreboardUI;

    private PlayerControls playerControls;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
        }
        instance = this;
    }

    private void Start()
    {
        GameManager.Instance.onGameStateChanged += HandleGameStateChanged;
        playerControls = new PlayerControls();
        playerControls.asset.Enable();
    }

    private void HandleGameStateChanged(GameState currentState)
    {
        if (currentState == GameState.Initialising)
        {
            StartCoroutine(InitializeDelayed());
        }
    }

    private IEnumerator InitializeDelayed()
    {
        yield return new WaitForSeconds(0.4f);

        scoreboardUI.Initialize();
        scoreboardUI.CloseScoreboard();
    }

    private void Update()
    {
        if (playerControls.UI.OpenScoreboard.WasPressedThisFrame())
        {
            scoreboardUI.OpenScoreboard();
        }
        else if (playerControls.UI.CloseScoreboard.WasPressedThisFrame())
        {
            scoreboardUI.CloseScoreboard();
        }
    }

    public void ShowScore(int amount, bool orangeTeam, Sprite portrait)
    {
        ScoreUIInfo scoreInfo = new ScoreUIInfo(amount, portrait);

        if (orangeTeam)
        {
            orangeScoreUI.EnqueueScore(scoreInfo);
        }
        else
        {
            blueScoreUI.EnqueueScore(scoreInfo);
        }
    }

    public void SetEnergyBallState(bool isPressed)
    {
        energyUI.SetBallPressed(isPressed);
    }

    public void SetEnergyBallLock(bool isLocked)
    {
        energyUI.SetLockIcon(isLocked);
    }

    public void InitializeMoveLearnPanel(MoveAsset[] moves)
    {
        moveLearnPanel.EnqueueNewMove(moves);
    }

    public void ShowKill(DamageInfo info, Pokemon killed)
    {
        KillInfo killInfo = new KillInfo(info, killed);
        killNotificationUI.EnqueueKill(killInfo);
    }

    public void ShowDeathScreen()
    {
        deathScreenUI.gameObject.SetActive(true);
    }

    public void HideDeathScreen()
    {
        deathScreenUI.gameObject.SetActive(false);
    }

    public void UpdateDeathScreenTimer(int time)
    {
        deathScreenUI.UpdateTimerText(time);
    }

    public void ShowMoveCooldown(int id, float time)
    {
        moveUIs[id].StartCooldown(time);
    }

    public void ShowMoveSecondaryCooldown(int id, float time)
    {
        moveUIs[id].ShowSecondaryCooldown(time);
    }

    public void ShowBattleItemCooldown(float time)
    {
        battleItemUI.StartCooldown(time);
    }

    public void UpdateUniteMoveCooldown(int currCharge, int maxCharge)
    {
        uniteMoveUI.UpdateUI(currCharge, maxCharge);
    }

    public void SetUniteMoveDisabledLock(bool visible)
    {
        uniteMoveUI.SetDisabledLock(visible);
    }

    public void SetMoveLock(int id, bool isLocked)
    {
        moveUIs[id].SetLock(isLocked);
    }

    public void SetBattleItemLock(bool locked)
    {
        battleItemUI.SetLock(locked);
    }

    public void UpdateEnergyUI(int currEnergy, int maxEnergy)
    {
        energyUI.UpdateEnergyUI(currEnergy, maxEnergy);
    }

    public void UpdateScoreGauge(float currTime, float maxTime)
    {
        energyUI.UpdateScoreGauge(currTime, maxTime);
    }

    public void UpdateGoalState(bool orangeTeam)
    {
        goalStateUI.UpdateGoalState(orangeTeam);
    }

    public void InitializeMoveUI(MoveAsset move)
    {
        switch (move.moveType)
        {
            case MoveType.MoveA:
                moveUIs[0].Initialize(move);
                break;
            case MoveType.MoveB:
                moveUIs[1].Initialize(move);
                break;
            case MoveType.UniteMove:
                uniteMoveUI.Initialize(move);
                break;
            case MoveType.All:
                for (int i = 0; i < moveUIs.Length; i++)
                {
                    moveUIs[i].Initialize(move);
                }
                uniteMoveUI.Initialize(move);
                break;
            default:
                break;
        }
    }

    public void InitializeBattleItemUI(BattleItemAsset battleItem)
    {
        battleItemUI.Initialize(battleItem);
    }

    public void UpdateRecallBar(float value)
    {
        recallBarUI.SetRecallBar(value);
    }

    public void SetRecallBarActive(bool active)
    {
        recallBarUI.SetRecallBarActive(active);
    }
}
