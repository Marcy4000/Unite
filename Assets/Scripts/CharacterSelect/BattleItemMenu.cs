using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleItemMenu : MonoBehaviour
{
    [SerializeField] private GameObject battleItemElementPrefab;
    [SerializeField] private Transform battleItemsHolder;
    [SerializeField] private ToggleGroup battleItemToggleGroup;
    [SerializeField] private TMP_Text battleItemName, battleItemDescription;

    private BattleItemAsset selectedBattleItem;

    private void Start()
    {
        selectedBattleItem = CharactersList.instance.GetBattleItemByID(0);

        foreach (var battleItem in CharactersList.instance.BattleItems)
        {
            if (battleItem.battleItemType == AvailableBattleItems.None)
            {
                continue;
            }

            var battleItemElement = Instantiate(battleItemElementPrefab, battleItemsHolder);
            battleItemElement.GetComponent<BattleItemElementUI>().Initialize(battleItem);
            Toggle toggle = battleItemElement.GetComponent<Toggle>();
            toggle.group = battleItemToggleGroup;
            toggle.onValueChanged.AddListener((value) =>
            {
                if (value)
                {
                    selectedBattleItem = battleItem;
                    battleItemName.text = battleItem.itemName;
                    battleItemDescription.text = battleItem.description;
                }
            });
        }

        battleItemToggleGroup.SetAllTogglesOff();
        CancelAndExit();
    }

    public void SaveAndExit()
    {
        int battleItemID = (int)selectedBattleItem.battleItemType;
        LobbyController.Instance.ChangePlayerBattleItem(battleItemID.ToString());
        gameObject.SetActive(false);
    }

    public void CancelAndExit()
    {
        gameObject.SetActive(false);
    }

    public void Open()
    {
        gameObject.SetActive(true);
    }
}
