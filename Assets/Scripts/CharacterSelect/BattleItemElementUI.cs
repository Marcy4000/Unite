using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleItemElementUI : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemName;
    private BattleItemAsset battleItem;

    public BattleItemAsset BattleItem => battleItem;

    public void Initialize(BattleItemAsset battleItem)
    {
        this.battleItem = battleItem;
        itemIcon.sprite = battleItem.icon;
        itemName.text = battleItem.itemName;
    }
}
