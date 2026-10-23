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
        StopAllCoroutines();
        ReleaseIcon();
        this.item = item;
        itemName.text = item != null ? item.itemName : "";

        if (isActiveAndEnabled) StartIconLoad();
    }

    private void OnEnable() => StartIconLoad();

    private void StartIconLoad()
    {
        if (iconHandle.IsValid() || item == null || item.sprite == null || !item.sprite.RuntimeKeyIsValid()) return;
        StartCoroutine(LoadIcon());
    }

    private IEnumerator LoadIcon()
    {
        iconHandle = Addressables.LoadAssetAsync<Sprite>(item.sprite);

        yield return iconHandle;

        if (iconHandle.Status == AsyncOperationStatus.Succeeded)
        {
            itemIcon.sprite = iconHandle.Result;
            itemIcon.enabled = itemIcon.sprite != null;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ReleaseIcon();
    }

    private void ReleaseIcon()
    {
        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.enabled = false;
        }
        if (iconHandle.IsValid())
        {
            Addressables.Release(iconHandle);
        }
        iconHandle = default;
    }

    private void OnDestroy() => ReleaseIcon();
}
