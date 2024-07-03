using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatHolderUI : MonoBehaviour
{
    [SerializeField] private TMP_Text damageAmount, damagePercentage;
    [SerializeField] private Slider damageSlider;

    public void SetStatInfo(float amount, float percentage)
    {
        damageAmount.text = amount.ToString();
        float percentageRounded = Mathf.Round(percentage * 100f);
        damagePercentage.text = $"{percentageRounded}%";
        damageSlider.value = percentage;
    }
}
