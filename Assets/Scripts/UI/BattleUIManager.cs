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

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
        }
        instance = this;
    }

    public void InitializeMoveLearnPanel(MoveAsset[] moves)
    {
        moveLearnPanel.EnqueueNewMove(moves);
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
