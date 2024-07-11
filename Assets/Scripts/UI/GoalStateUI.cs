using UnityEngine;

public class GoalStateUI : MonoBehaviour
{
    [SerializeField] private GameObject[] blueDots;
    [SerializeField] private GameObject[] orangeDots;

    private int blueActiveGoals = 4;
    private int orangeActiveGoals = 4;

    public void UpdateGoalState(bool orangeTeam)
    {
        if (orangeTeam)
        {
            orangeActiveGoals--;
        }
        else
        {
            blueActiveGoals--;
        }

        UpdateGoalUI();
    }

    private void UpdateGoalUI()
    {
        for (int i = 0; i < blueDots.Length; i++)
        {
            blueDots[i].SetActive(i < blueActiveGoals);
        }

        for (int i = 0; i < orangeDots.Length; i++)
        {
            orangeDots[i].SetActive(i < orangeActiveGoals);
        }
    }
}
