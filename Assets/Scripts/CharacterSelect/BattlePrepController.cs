using UnityEngine;

public class BattlePrepController : MonoBehaviour
{
    [SerializeField] private GameObject battlePrepHolder;

    private void Start()
    {
        HideBattlePrep();
    }

    public void ShowBattlePrep()
    {
        battlePrepHolder.SetActive(true);
    }

    public void HideBattlePrep()
    {
        battlePrepHolder.SetActive(false);
    }
}
