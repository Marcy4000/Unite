using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CurrentItemsMenu : MonoBehaviour
{
    [SerializeField] private TMP_Text itemName, itemDescription;
    [SerializeField] private ToggleGroup iconHolder;

    [SerializeField] private GameObject statBoostPrefab;
    [SerializeField] private Transform statBoostsHolder;

    [SerializeField] private GameObject itemIconPrefab;
    [SerializeField] private HeldItemPicker heldItemPicker;

    private List<HeldItemInfo> selectedHeldItems;

    public List<HeldItemInfo> SelectedHeldItems => selectedHeldItems;

    public System.Action OnItemsChanged;

    private void Start()
    {
        heldItemPicker.OnItemPicked += UpdateSelectedItem;
    }

    public void InitializeIcons(List<HeldItemInfo> heldItems)
    {
        selectedHeldItems = heldItems;

        foreach (Transform child in iconHolder.transform)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < heldItems.Count; i++)
        {
            var obj = Instantiate(itemIconPrefab, iconHolder.transform);
            var itemIcon = obj.GetComponent<HeldItemIcon>();

            itemIcon.Toggle.group = iconHolder;
            itemIcon.InitializeItem(heldItems[i]);

            itemIcon.Toggle.onValueChanged.AddListener(OnSelectedHeldItemChange);
        }

        StartCoroutine(InitializeItemPicker(heldItems[0]));
    }

    private void OnSelectedHeldItemChange(bool value)
    {
        if (value)
        {
            var activeToggle = iconHolder.GetFirstActiveToggle();
            var itemIcon = activeToggle.GetComponent<HeldItemIcon>();

            itemName.text = itemIcon.ItemInfo.heldItemName;
            itemDescription.text = itemIcon.ItemInfo.description;

            InitializeStatBoost(itemIcon.ItemInfo);

            StartCoroutine(InitializeItemPicker(null));
        }
    }

    private void InitializeStatBoost(HeldItemInfo itemInfo)
    {
        foreach (Transform child in statBoostsHolder)
        {
            Destroy(child.gameObject);
        }

        foreach (var statBoost in itemInfo.statBoosts)
        {
            var obj = Instantiate(statBoostPrefab, statBoostsHolder);
            var statBoostUI = obj.GetComponent<ItemStatBoostUI>();

            statBoostUI.Initialize(statBoost);
        }
    }

    private IEnumerator InitializeItemPicker(HeldItemInfo heldItem)
    {
        yield return null;

        HeldItemInfo selectedItem = heldItem ?? heldItemPicker.GetSelectedItem();

        heldItemPicker.InitializeIcons(selectedItem);
    }

    private void UpdateSelectedItem(HeldItemInfo itemInfo)
    {
        HeldItemIcon selectedIcon = iconHolder.GetFirstActiveToggle().GetComponent<HeldItemIcon>();
        int selectedIconID = selectedIcon.Toggle.transform.GetSiblingIndex();

        if (CheckForDuplicate(itemInfo))
        {
            return;
        }

        selectedHeldItems[selectedIconID] = itemInfo;

        selectedIcon.InitializeItem(itemInfo);
        OnItemsChanged?.Invoke();
        OnSelectedHeldItemChange(true);
    }

    private bool CheckForDuplicate(HeldItemInfo itemInfo)
    {
        if (itemInfo.heldItemID == AvailableHeldItems.None)
        {
            return false;
        }

        foreach (var item in selectedHeldItems)
        {
            if (item == itemInfo)
            {
                return true;
            }
        }

        return false;
    }
}
