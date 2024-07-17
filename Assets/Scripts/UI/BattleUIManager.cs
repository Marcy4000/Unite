using System.Collections;
using UnityEngine;

public class BattleUIManager : MonoBehaviour
{
    public static BattleUIManager instance;

    [SerializeField] private MovesHolderUI desktopUI;
    [SerializeField] private MovesHolderUI mobileUI;
    [SerializeField] private ScoreUI blueScoreUI, orangeScoreUI;
    [SerializeField] private DeathScreenUI deathScreenUI;
    [SerializeField] private KillNotificationUI killNotificationUI;
    [SerializeField] private GoalStateUI goalStateUI;
    [SerializeField] private RecallBarUI recallBarUI;
    [SerializeField] private ScoreboardUI scoreboardUI;
    [SerializeField] private SurrenderTextbox surrenderTextbox;

    private MovesHolderUI currentUI;

    private PlayerControls playerControls;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
        }
        instance = this;

#if UNITY_ANDROID
        currentUI = mobileUI;
        mobileUI.gameObject.SetActive(true);
        Destroy(desktopUI.gameObject);
#else
        currentUI = desktopUI;
        Destroy(mobileUI.gameObject);
        desktopUI.gameObject.SetActive(true);
#endif
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
        scoreboardUI.CloseScoreboard();
        yield return new WaitUntil(() => GameManager.Instance.GameState == GameState.Playing);

        scoreboardUI.OpenScoreboard();
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
        currentUI.SetEnergyBallState(isPressed);
    }

    public void SetEnergyBallLock(bool isLocked)
    {
        currentUI.SetEnergyBallLock(isLocked);
    }

    public void InitializeMoveLearnPanel(MoveAsset[] moves)
    {
        currentUI.InitializeMoveLearnPanel(moves);
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
        currentUI.ShowMoveCooldown(id, time);
    }

    public void ShowMoveSecondaryCooldown(int id, float time)
    {
        currentUI.ShowMoveSecondaryCooldown(id, time);
    }

    public void ShowBattleItemCooldown(float time)
    {
        currentUI.ShowBattleItemCooldown(time);
    }

    public void UpdateUniteMoveCooldown(int currCharge, int maxCharge)
    {
        currentUI.UpdateUniteMoveCooldown(currCharge, maxCharge);
    }

    public void SetUniteMoveDisabledLock(bool visible)
    {
        currentUI.SetUniteMoveDisabledLock(visible);
    }

    public void SetMoveLock(int id, bool isLocked)
    {
        currentUI.SetMoveLock(id, isLocked);
    }

    public void SetBattleItemLock(bool locked)
    {
        currentUI.SetBattleItemLock(locked);
    }

    public void UpdateEnergyUI(int currEnergy, int maxEnergy)
    {
        currentUI.UpdateEnergyUI(currEnergy, maxEnergy);
    }

    public void UpdateScoreGauge(float currTime, float maxTime)
    {
        currentUI.UpdateScoreGauge(currTime, maxTime);
    }

    public void UpdateGoalState(bool orangeTeam)
    {
        goalStateUI.UpdateGoalState(orangeTeam);
    }

    public void InitializeMoveUI(MoveAsset move)
    {
        currentUI.InitializeMoveUI(move);
    }

    public void InitializeBattleItemUI(BattleItemAsset battleItem)
    {
        currentUI.InitializeBattleItemUI(battleItem);
    }

    public void UpdateRecallBar(float value)
    {
        recallBarUI.SetRecallBar(value);
    }

    public void SetRecallBarActive(bool active)
    {
        recallBarUI.SetRecallBarActive(active);
    }

    public void ShowSurrenderTextbox(bool yourTeamSurrendered)
    {
        surrenderTextbox.ShowSurrenderTextbox(yourTeamSurrendered);
    }
}
