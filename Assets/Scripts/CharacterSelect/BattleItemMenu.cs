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
        selectedBattleItem = CharactersList.Instance.GetBattleItemByID(0);

        foreach (var battleItem in CharactersList.Instance.BattleItems)
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
                    battleItemDescription.SetText(battleItem.description);
                }
            });
        }

        battleItemToggleGroup.SetAllTogglesOff();
        CancelAndExit();
    }

    private void OnEnable()
    {
        int selectedBattleItemId = int.Parse(LobbyController.Instance.Player.Data["BattleItem"].Value);
        selectedBattleItem = CharactersList.Instance.GetBattleItemByID(selectedBattleItemId);
        battleItemName.text = selectedBattleItem.itemName;
        battleItemDescription.text = selectedBattleItem.description;

        foreach (Transform child in battleItemsHolder)
        {
            BattleItemElementUI battleItemElement = child.GetComponent<BattleItemElementUI>();
            if (battleItemElement.BattleItem.battleItemType == selectedBattleItem.battleItemType)
            {
                battleItemElement.GetComponent<Toggle>().isOn = true;
            }
        }
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
