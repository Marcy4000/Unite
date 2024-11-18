using UnityEngine;

public class MovesHolderUI : MonoBehaviour
{
    [SerializeField] private MoveUI[] moveUIs;
    [SerializeField] private UniteMoveUI uniteMoveUI;
    [SerializeField] private MoveLearnPanel moveLearnPanel;
    [SerializeField] private BattleItemUI battleItemUI;
    [SerializeField] private EnergyUI energyUI;

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

    public void ShowMoveCooldown(int id, float time, float maxCdDuration)
    {
        moveUIs[id].StartCooldown(time, maxCdDuration);
    }

    public void ShowMoveSecondaryCooldown(int id, float time)
    {
        moveUIs[id].ShowSecondaryCooldown(time);
    }

    public void ShowUniteMoveSecondaryCooldown(float time)
    {
        uniteMoveUI.ShowSecondaryCooldown(time);
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
}
