using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleInfoUI : MonoBehaviour
{
    [SerializeField] private TMP_Text[] blueTeamScore, orangeTeamScore;
    [SerializeField] private Toggle[] menuOptions;
    [SerializeField] private BattleInfoMenuUI[] menuPanels;

    [SerializeField] private bool relativeMode = false;

    public void Initialize(GameResults gameResults)
    {
        foreach (var panel in menuPanels)
        {
            panel.relativeMode = relativeMode;
            panel.InitializeMenu();
        }

        SetScore(gameResults.BlueTeamScore, gameResults.OrangeTeamScore);

        for (int i = 0; i < menuOptions.Length; i++)
        {
            int index = i;
            menuOptions[i].onValueChanged.AddListener((value) => SetMenuPanel(index));
        }

        ClosePanel();
    }

    public void SetScore(int blueScore, int orangeScore)
    {
        foreach (var score in blueTeamScore)
        {
            score.text = blueScore.ToString();
        }

        foreach (var score in orangeTeamScore)
        {
            score.text = orangeScore.ToString();
        }
    }

    public void SetMenuPanel(int index)
    {
        for (int i = 0; i < menuPanels.Length; i++)
        {
            menuPanels[i].gameObject.SetActive(i == index);
        }
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
}
