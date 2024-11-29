using TMPro;
using UnityEngine;

public class ItemStatBoostUI : MonoBehaviour
{
    [SerializeField] private TMP_Text statName;
    [SerializeField] private TMP_Text statValue;

    public void Initialize(HeldItemStatBoost statBoost)
    {
        statName.text = statBoost.AffectedStat.ToString();
        statValue.text = $"+{statBoost.BoostAmount}" + (statBoost.IsPercentage ? "%" : "");
    }
}
