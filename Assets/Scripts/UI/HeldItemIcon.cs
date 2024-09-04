using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeldItemIcon : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemLevelText;
    [SerializeField] private Toggle toggle;

    private HeldItemInfo itemInfo;

    public Toggle Toggle => toggle;
    public HeldItemInfo ItemInfo => itemInfo;

    public void InitializeItem(HeldItemInfo itemInfo)
    {
        this.itemInfo = itemInfo;
        itemIcon.sprite = itemInfo.icon;
        itemLevelText.text = "30"; // Levels not available yet
    }
}
