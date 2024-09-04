using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HeldItemPicker : MonoBehaviour
{
    [SerializeField] private GameObject itemIconPrefab;
    [SerializeField] private Transform iconHolder;

    [SerializeField] private TMP_Text itemName, itemDescription;

    [SerializeField] private ToggleGroup toggleGroup;

    public System.Action<HeldItemInfo> OnItemPicked;

    public void InitializeIcons(HeldItemInfo selectedItem)
    {
        foreach (Transform child in iconHolder)
        {
            Destroy(child.gameObject);
        }

        foreach (var item in CharactersList.Instance.HeldItems)
        {
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
}
