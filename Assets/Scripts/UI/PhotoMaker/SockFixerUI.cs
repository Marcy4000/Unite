using UnityEngine;
using System.IO;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.AddressableAssets;




#if UNITY_EDITOR
using UnityEditor;
#endif

public class SockFixerUI : MonoBehaviour
{
    [Tooltip("Relative path from the Assets folder where the ClothingItem ScriptableObjects are stored.")]
    public string directoryPath = "ScriptableObjects/ClothingItems";

    private List<ClothingItem> clothingItems = new List<ClothingItem>();
    private int currentItemIndex = 0;
    private bool waitingForInput = false;

    [SerializeField] private Image _image;
    [SerializeField] private Transform modelSpawnPoint;

    private AsyncOperationHandle<Sprite> _handle;
    private AsyncOperationHandle<GameObject> _modelHandle;

    private GameObject model;

    void Start()
    {
        LoadClothingItems();
        if (clothingItems.Count > 0)
        {
            PromptNextItem();
        }
    }

    void Update()
    {
        if (!waitingForInput || currentItemIndex >= clothingItems.Count)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (ProcessClothingItem(clothingItems[currentItemIndex], 0))
                NextItem();
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (ProcessClothingItem(clothingItems[currentItemIndex], 1))
                NextItem();
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            if (ProcessClothingItem(clothingItems[currentItemIndex], 2))
                NextItem();
        }
    }

    private void LoadClothingItems()
    {
#if UNITY_EDITOR
        string fullPath = Path.Combine(Application.dataPath, directoryPath);
        if (!Directory.Exists(fullPath))
        {
            Debug.LogError($"Directory does not exist: {fullPath}");
            return;
        }

        string[] assetPaths = Directory.GetFiles(fullPath, "*.asset", SearchOption.AllDirectories);
        clothingItems.Clear();

        foreach (string assetPath in assetPaths)
        {
            string relativePath = "Assets" + assetPath.Replace(Application.dataPath, "").Replace("\\", "/");
            ClothingItem item = AssetDatabase.LoadAssetAtPath<ClothingItem>(relativePath);
            if (item != null)
            {
                clothingItems.Add(item);
            }
        }

        Debug.Log($"Loaded {clothingItems.Count} ClothingItems.");
#else
        Debug.LogError("This script works only in the Unity Editor.");
#endif
    }

    private void PromptNextItem()
    {
        if (currentItemIndex < clothingItems.Count)
        {
            Debug.Log($"Processing {clothingItems[currentItemIndex].name}. Press 1, 2, or 3 to choose an action.");
            waitingForInput = true;

            if (_handle.IsValid())
            {
                Addressables.Release(_handle);
            }

            if (_modelHandle.IsValid())
            {
                Addressables.Release(_modelHandle);

                if (model != null)
                {
                    Destroy(model);
                }
            }

            if (clothingItems[currentItemIndex].sprite.RuntimeKeyIsValid())
            {
                _handle = Addressables.LoadAssetAsync<Sprite>(clothingItems[currentItemIndex].sprite);

                _handle.Completed += OnSpriteLoaded;
            }
            
            if (clothingItems[currentItemIndex].prefabs.Count > 0 && clothingItems[currentItemIndex].prefabs[0].RuntimeKeyIsValid())
            {
                _modelHandle = Addressables.LoadAssetAsync<GameObject>(clothingItems[currentItemIndex].prefabs[0]);

                _modelHandle.Completed += (handle) =>
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        model = Instantiate(handle.Result, modelSpawnPoint);
                        model.transform.localPosition = Vector3.zero;
                        model.transform.localRotation = Quaternion.identity;
                        model.transform.localScale = Vector3.one;
                    }
                };
            }


        }
        else
        {
            Debug.Log("Finished processing all items.");
#if UNITY_EDITOR
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
        }
    }

    private void OnSpriteLoaded(AsyncOperationHandle<Sprite> handle)
    {
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _image.sprite = handle.Result;
        }
    }

    private void NextItem()
    {
        waitingForInput = false;
        currentItemIndex++;
        PromptNextItem();
    }

    private bool ProcessClothingItem(ClothingItem item, int option)
    {
        // Implement your custom logic based on the input option
        Debug.Log($"Processing {item.name} with option {option}.");
        // Example: Modify a property based on the option
        // if (option == 1) item.color = Color.red;

        if (item.isMale && option > 1)
        {
            return false;
        }

        item.sockTypeToUse = option;

#if UNITY_EDITOR
        EditorUtility.SetDirty(item);
#endif

        return true;
    }
}
