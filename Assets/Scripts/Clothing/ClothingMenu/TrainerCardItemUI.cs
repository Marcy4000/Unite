using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

public class TrainerCardItemUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private Toggle toggle;

    private TrainerCardItem item;
    private AsyncOperationHandle<Sprite> handle;

    public Toggle Toggle => toggle;

    public void Initialize(TrainerCardItem item)
    {
        this.item = item;
        StartCoroutine(LoadIcon());
    }

    private IEnumerator LoadIcon()
    {
        handle = Addressables.LoadAssetAsync<Sprite>(item.itemIcon);
        yield return handle;

        if (handle.Status == AsyncOperationStatus.Succeeded)
            icon.sprite = handle.Result;
    }

    private void OnDestroy()
    {
        Addressables.Release(handle);
    }
}
