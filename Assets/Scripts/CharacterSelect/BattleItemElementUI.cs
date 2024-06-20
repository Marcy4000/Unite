using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleItemElementUI : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemName;

    public void Initialize(BattleItemAsset battleItem)
    {
        itemIcon.sprite = battleItem.icon;
        itemName.text = battleItem.itemName;
    }
}
