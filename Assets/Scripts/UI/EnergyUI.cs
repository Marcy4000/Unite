using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnergyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text currEnergyText, maxEnergyText;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private Image ballIcon, scoreGauge;
    [SerializeField] private Sprite idle, pressed;

    private void Start()
    {
        scoreGauge.fillAmount = 0;
        lockIcon.SetActive(false);
    }

    public void SetLockIcon(bool value)
    {
        lockIcon.SetActive(value);
    }

    public void SetBallPressed(bool value)
    {
        if (value)
        {
            ballIcon.sprite = pressed;
        }
        else
        {
            ballIcon.sprite = idle;
        }
    }

    public void UpdateEnergyUI(int currEnergy, int maxEnergy)
    {
        currEnergyText.text = currEnergy.ToString();
        maxEnergyText.text = maxEnergy.ToString();
    }

    public void UpdateEnergyUI(int currEnergy)
    {
        currEnergyText.text = currEnergy.ToString();
    }

    public void UpdateScoreGauge(float currTime, float maxTime)
    {
        scoreGauge.fillAmount = (float)currTime / maxTime;
    }
}
