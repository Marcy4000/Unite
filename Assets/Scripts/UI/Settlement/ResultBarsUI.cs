using UnityEngine;
using UnityEngine.UI;

public class ResultBarsUI : MonoBehaviour
{
    [SerializeField] private Image blueBar, orangeBar;
    private int maxPoints;

    public void InitializeUI(int maxPoints)
    {
        this.maxPoints = maxPoints;
        blueBar.fillAmount = 0f;
        orangeBar.fillAmount = 0f;
    }

    public void SetBars(int blueValue, int orangeValue)
    {
        blueBar.fillAmount = (float)blueValue / maxPoints;
        orangeBar.fillAmount = (float)orangeValue / maxPoints;
    }
}
