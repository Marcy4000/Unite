using UnityEngine;
using UnityEngine.UI;

public class ResultBarsUI : MonoBehaviour
{
    [SerializeField] private Image blueBar, orangeBar;
    private int maxPoints;

    public void InitializeUI(int maxPoints)
    {
        this.maxPoints = maxPoints;

        if (maxPoints == 0)
        {
            blueBar.transform.localScale = Vector3.zero;
            orangeBar.transform.localScale = Vector3.zero;
            return;
        }

        SetBars(0, 0);
    }

    public void SetBars(int blueValue, int orangeValue)
    {
        if (maxPoints == 0)
        {
            blueBar.transform.localScale = Vector3.zero;
            orangeBar.transform.localScale = Vector3.zero;
            return;
        }

        float blueScaleY = (float)blueValue / maxPoints;
        float orangeScaleY = (float)orangeValue / maxPoints;

        blueBar.transform.localScale = new Vector3(blueBar.transform.localScale.x, blueScaleY, blueBar.transform.localScale.z);
        orangeBar.transform.localScale = new Vector3(orangeBar.transform.localScale.x, orangeScaleY, orangeBar.transform.localScale.z);
    }
}
