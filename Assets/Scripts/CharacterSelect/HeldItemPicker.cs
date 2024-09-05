using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeldItemPicker : MonoBehaviour
{
    [SerializeField] private GameObject itemIconPrefab;
    [SerializeField] private Transform iconHolder;

    [SerializeField] private TMP_Text itemName, itemDescription;

    [SerializeField] private ToggleGroup toggleGroup;
    [SerializeField] private TMP_Dropdown categoryDropdown;

    public System.Action<HeldItemInfo> OnItemPicked;

    public void InitializeIcons(HeldItemInfo selectedItem)
    {
        foreach (Transform child in iconHolder)
        {
            Destroy(child.gameObject);
        }

        foreach (var item in CharactersList.Instance.HeldItems)
        {
            if (categoryDropdown.value != 0 && item.heldItemID != AvailableHeldItems.None)
            {
                if (categoryDropdown.value >= 1 && categoryDropdown.value <= 2)
                {
                    if ((int)item.damageType != (categoryDropdown.value - 1))
                    {
                        continue;
                    }
                }
                else if (categoryDropdown.value > 2)
                {
                    if ((int)item.heldItemCategory != (categoryDropdown.value - 3))
                    {
                        continue;
                    }
                }
            }

            var obj = Instantiate(itemIconPrefab, iconHolder);
            var itemIcon = obj.GetComponent<HeldItemIcon>();

            itemIcon.Toggle.group = toggleGroup;
            itemIcon.InitializeItem(item);

            itemIcon.Toggle.onValueChanged.AddListener(OnActiveToggleChanged);

            if (selectedItem != null && selectedItem == item)
            {
                itemIcon.Toggle.isOn = true;
            }
        }
    }

    private void OnActiveToggleChanged(bool value)
    {
        if (value)
        {
            var activeToggle = toggleGroup.GetFirstActiveToggle();
            var itemIcon = activeToggle.GetComponent<HeldItemIcon>();

            itemName.text = itemIcon.ItemInfo.heldItemName;
            itemDescription.text = itemIcon.ItemInfo.description;
        }
    }

    public void OnConfirm()
    {
        var activeToggle = toggleGroup.GetFirstActiveToggle();
        var itemIcon = activeToggle.GetComponent<HeldItemIcon>();

        OnItemPicked?.Invoke(itemIcon.ItemInfo);
    }

    public HeldItemInfo GetSelectedItem()
    {
        var activeToggle = toggleGroup.GetFirstActiveToggle();
        var itemIcon = activeToggle.GetComponent<HeldItemIcon>();

        return itemIcon.ItemInfo;
    }
}
