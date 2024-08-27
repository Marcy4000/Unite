using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

public class ClothingSelectionItem : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemName;
    [SerializeField] private Toggle itemToggle;

    private ClothingItem item;

    public ClothingItem Item => item;
    public Toggle ItemToggle => itemToggle;

    public void SetItem(ClothingItem item)
    {
        this.item = item;
        itemName.text = item.itemName;

        if (!item.sprite.RuntimeKeyIsValid())
        {
            return;
        }

        itemIcon.sprite = Addressables.LoadAssetAsync<Sprite>(item.sprite).WaitForCompletion();
    }
}
