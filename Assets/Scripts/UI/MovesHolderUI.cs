using UnityEngine;

public class MovesHolderUI : MonoBehaviour
{
    [SerializeField] private MoveUI[] moveUIs;
    [SerializeField] private UniteMoveUI uniteMoveUI;
    [SerializeField] private MoveLearnPanel moveLearnPanel;
    [SerializeField] private BattleItemUI battleItemUI;
    [SerializeField] private EnergyUI energyUI;

    [Header("Jump Mode (Mobile Only)")]
    [SerializeField] private GameObject normalButtonsHolder; // Parent of normal move buttons
    [SerializeField] private GameObject jumpModeOverlay; // Overlay shown during jump selection

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

    public void ShowBattleItemCooldown(float time, float maxCdDuration)
    {
        battleItemUI.StartCooldown(time, maxCdDuration);
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

    /// <summary>
    /// Shows the jump button on mobile when player enters a jump pad.
    /// The button will trigger the same input as the Recall button.
    /// </summary>
    public void ShowJumpButton()
    {
        if (normalButtonsHolder != null)
        {
            normalButtonsHolder.SetActive(false);
        }

        if (jumpModeOverlay != null)
        {
            jumpModeOverlay.SetActive(true);
        }
    }

    /// <summary>
    /// Hides the jump button and restores normal buttons when player exits jump pad.
    /// </summary>
    public void HideJumpButton()
    {
        if (normalButtonsHolder != null)
        {
            normalButtonsHolder.SetActive(true);
        }

        if (jumpModeOverlay != null)
        {
            jumpModeOverlay.SetActive(false);
        }
    }
}
