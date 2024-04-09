using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleUIManager : MonoBehaviour
{
    public static BattleUIManager instance;

    [SerializeField] private MoveUI[] moveUIs;
    [SerializeField] private UniteMoveUI uniteMoveUI;
    [SerializeField] private MoveLearnPanel moveLearnPanel;
    [SerializeField] private EnergyUI energyUI;
    [SerializeField] private ScoreUI blueScoreUI, orangeScoreUI;
    [SerializeField] private DeathScreenUI deathScreenUI;
    [SerializeField] private KillNotificationUI killNotificationUI;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
        }
        instance = this;
    }

    public void ShowScore(int amount, bool orangeTeam)
    {
        if (orangeTeam)
        {
            orangeScoreUI.ShowScore(amount);
        }
        else
        {
            blueScoreUI.ShowScore(amount);
        }
    }

    public void SetEnergyBallState(bool isPressed)
    {
        energyUI.SetBallPressed(isPressed);
    }

    public void InitializeMoveLearnPanel(MoveAsset[] moves)
    {
        moveLearnPanel.EnqueueNewMove(moves);
    }

    public void ShowKill(DamageInfo info, bool orangeTeam, Pokemon killed)
    {
        killNotificationUI.ShowKill(info, orangeTeam, killed);
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

    public void ReferenceController(MovesController controller)
    {
        uniteMoveUI.AssignController(controller);
    }

    public void UpdateEnergyUI(int currEnergy, int maxEnergy)
    {
        energyUI.UpdateEnergyUI(currEnergy, maxEnergy);
    }

    public void UpdateScoreGauge(float currTime, float maxTime)
    {
        energyUI.UpdateScoreGauge(currTime, maxTime);
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
}
