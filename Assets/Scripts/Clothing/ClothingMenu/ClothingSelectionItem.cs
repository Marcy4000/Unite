using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class ClothingSelectionItem : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemName;
    [SerializeField] private Toggle itemToggle;

    private ClothingItem item;

    public ClothingItem Item => item;
    public Toggle ItemToggle => itemToggle;

    private AsyncOperationHandle<Sprite> iconHandle;

    public void SetItem(ClothingItem item)
    {
        this.item = item;
        itemName.text = item.itemName;

        if (!item.sprite.RuntimeKeyIsValid())
        {
            return;
        }

        StartCoroutine(LoadIcon());
    }

    private IEnumerator LoadIcon()
    {
        iconHandle = Addressables.LoadAssetAsync<Sprite>(item.sprite);

        yield return iconHandle;

        if (iconHandle.Status == AsyncOperationStatus.Succeeded)
        {
            itemIcon.sprite = iconHandle.Result;
        }
    }

    private void OnDestroy()
    {
        if (iconHandle.IsValid())
        {
            Addressables.Release(iconHandle);
        }
    }
}
