using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.AddressableAssets;
using Newtonsoft.Json;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using Unity.VisualScripting;

public class ClothingItemGeneratorNew : MonoBehaviour
{
    private static string jsonFilePath = "Assets/Clothes/AvatarItems.json";
    private static string clothesIconsPath = "Assets/Clothes/ElChicoEevee_files/index_data";
    private static string modelBasePath = "Assets/Model/Clothing";

    // Chunk processing configuration
    private const int CHUNK_SIZE = 50; // Process items in chunks to avoid RAM issues
    private const bool AGGRESSIVE_MEMORY_CLEANUP = true; // Force GC after each chunk

    // EditorPrefs keys
    private const string OverwritePrefKey = "ClothingGeneratorNew_OverwriteExisting";
    
    private static bool OverwriteExisting
    {
        get => EditorPrefs.GetBool(OverwritePrefKey, true);
        set
        {
            EditorPrefs.SetBool(OverwritePrefKey, value);
            Menu.SetChecked("Tools/Generate Clothing Items New/Overwrite Existing", value);
        }
    }

    // JSON data structures
    [Serializable]
    private class AvatarItemData
    {
        public string AvatarSlotType;
        public string AvatarName;
        public string AvatarIconMale;
        public string AvatarIconFemale;
        public PrefabVariants AvatarPrefabMale;
        public PrefabVariants AvatarPrefabFemale;
    }

    [Serializable]
    private class PrefabVariants
    {
        public string variant1;
        public string variant2;
        public string variant3;
    }

    // Slot type mapping from JSON values to ClothingType enum
    private static readonly Dictionary<string, ClothingType> slotTypeMapping = new Dictionary<string, ClothingType>(StringComparer.OrdinalIgnoreCase)
    {
        { "Top", ClothingType.Shirt },
        { "Outerwear", ClothingType.Overwear },
        { "Bottom", ClothingType.Pants },
        { "Backpack", ClothingType.Backpack },
        { "Hand", ClothingType.Gloves },
        { "Eye", ClothingType.Eyes },
        { "Hat", ClothingType.Hat },
        { "Socks", ClothingType.Socks },
        { "Shoes", ClothingType.Shoes },
        { "Suit", ClothingType.Shirt },
        { "Head", ClothingType.Face },
        { "Hair", ClothingType.Hair },
        { "Bodysuit", ClothingType.Shirt },
        { "Headwear", ClothingType.Hat }
    };

    // Clothing type to output folder mapping
    private static readonly Dictionary<ClothingType, string> outputFolderMapping = new Dictionary<ClothingType, string>
    {
        { ClothingType.Hair, "Hair" },
        { ClothingType.Overwear, "Overwear" },
        { ClothingType.Pants, "Pants" },
        { ClothingType.Gloves, "Gloves" },
        { ClothingType.Socks, "Socks" },
        { ClothingType.Eyes, "Eyes" },
        { ClothingType.Hat, "Hats" },
        { ClothingType.Shirt, "Shirt" },
        { ClothingType.Shoes, "Shoes" },
        { ClothingType.Face, "Face" },
        { ClothingType.Backpack, "Backpack" }
    };

    // Folder name mapping for disablesClothingType detection
    private static readonly Dictionary<string, ClothingType> folderMappings = new Dictionary<string, ClothingType>
    {
        { "Hair", ClothingType.Hair },
        { "Outwear", ClothingType.Overwear },
        { "Bottom", ClothingType.Pants },
        { "Hand", ClothingType.Gloves },
        { "Sock", ClothingType.Socks },
        { "Eye", ClothingType.Eyes },
        { "Hat", ClothingType.Hat },
        { "Top", ClothingType.Shirt },
        { "Foot", ClothingType.Shoes },
        { "Head", ClothingType.Face },
        { "Backpack", ClothingType.Backpack }
    };

    [InitializeOnLoadMethod]
    private static void InitMenuChecks()
    {
        Menu.SetChecked("Tools/Generate Clothing Items New/Overwrite Existing", OverwriteExisting);
    }

    [MenuItem("Tools/Generate Clothing Items New/Generate from JSON")]
    static void GenerateClothingItemsFromJson()
    {
        if (!File.Exists(jsonFilePath))
        {
            Debug.LogError($"JSON file not found at: {jsonFilePath}");
            return;
        }

        string jsonContent = File.ReadAllText(jsonFilePath);
        AvatarItemData[] avatarItems;

        try
        {
            avatarItems = JsonConvert.DeserializeObject<AvatarItemData[]>(jsonContent);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to parse JSON: {ex.Message}");
            return;
        }

        if (avatarItems == null || avatarItems.Length == 0)
        {
            Debug.LogWarning("No items found in JSON file.");
            return;
        }

        // Build processing queue (each item can generate 2 clothing items: male + female)
        List<(AvatarItemData item, ClothingType clothingType, bool isMale)> processingQueue = new List<(AvatarItemData, ClothingType, bool)>();

        foreach (var item in avatarItems)
        {
            if (!slotTypeMapping.TryGetValue(item.AvatarSlotType, out ClothingType clothingType))
            {
                Debug.LogWarning($"Unknown AvatarSlotType: {item.AvatarSlotType} for item {item.AvatarName}");
                continue;
            }

            // Queue male item
            if (item.AvatarPrefabMale != null && !string.IsNullOrEmpty(item.AvatarPrefabMale.variant1))
            {
                processingQueue.Add((item, clothingType, true));
            }

            // Queue female item
            if (item.AvatarPrefabFemale != null && !string.IsNullOrEmpty(item.AvatarPrefabFemale.variant1))
            {
                processingQueue.Add((item, clothingType, false));
            }
        }

        Debug.Log($"Starting chunked processing: {processingQueue.Count} total items in {Mathf.CeilToInt((float)processingQueue.Count / CHUNK_SIZE)} chunks");

        int successCount = 0;
        int skippedCount = 0;
        int totalItems = processingQueue.Count;
        bool cancelled = false;

        // Process in chunks
        for (int chunkStart = 0; chunkStart < totalItems; chunkStart += CHUNK_SIZE)
        {
            int chunkEnd = Mathf.Min(chunkStart + CHUNK_SIZE, totalItems);
            int currentChunk = (chunkStart / CHUNK_SIZE) + 1;
            int totalChunks = Mathf.CeilToInt((float)totalItems / CHUNK_SIZE);

            // Show progress bar (cancellable)
            float progress = (float)chunkStart / totalItems;
            if (EditorUtility.DisplayCancelableProgressBar(
                "Generating Clothing Items",
                $"Processing chunk {currentChunk}/{totalChunks} ({chunkStart}-{chunkEnd}/{totalItems} items)",
                progress))
            {
                cancelled = true;
                break;
            }

            // Process current chunk
            for (int i = chunkStart; i < chunkEnd; i++)
            {
                var (item, clothingType, isMale) = processingQueue[i];

                if (CreateClothingItem(item, clothingType, isMale))
                {
                    successCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            // Save and cleanup after each chunk to free memory
            AssetDatabase.SaveAssets();
            
            // Aggressive memory cleanup
            if (AGGRESSIVE_MEMORY_CLEANUP)
            {
                Resources.UnloadUnusedAssets();
                System.GC.Collect();
            }

            Debug.Log($"Chunk {currentChunk}/{totalChunks} complete. Created: {successCount}, Skipped: {skippedCount}");
        }

        // Final cleanup
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (cancelled)
        {
            Debug.LogWarning($"Generation cancelled by user. Partial results: Created: {successCount}, Skipped: {skippedCount}");
        }
        else
        {
            Debug.Log($"Clothing generation complete! Created: {successCount}, Skipped: {skippedCount}");
        }
    }

    private static bool CreateClothingItem(AvatarItemData item, ClothingType clothingType, bool isMale)
    {
        PrefabVariants prefabVariants = isMale ? item.AvatarPrefabMale : item.AvatarPrefabFemale;
        string iconName = isMale ? item.AvatarIconMale : item.AvatarIconFemale;
        string gender = isMale ? "Male" : "Female";

        // Collect all variant prefab paths
        List<string> variantNames = new List<string>();
        if (!string.IsNullOrEmpty(prefabVariants.variant1)) variantNames.Add(prefabVariants.variant1);
        if (!string.IsNullOrEmpty(prefabVariants.variant2)) variantNames.Add(prefabVariants.variant2);
        if (!string.IsNullOrEmpty(prefabVariants.variant3)) variantNames.Add(prefabVariants.variant3);

        if (variantNames.Count == 0)
        {
            Debug.LogWarning($"No variants found for {item.AvatarName} ({gender})");
            return false;
        }

        // Find all variant prefab paths
        List<string> prefabPaths = new List<string>();
        foreach (string variantName in variantNames)
        {
            string prefabPath = FindPrefabPath(variantName, gender);
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogWarning($"Prefab not found: {variantName} for {item.AvatarName} ({gender}) - SKIPPING ITEM");
                return false; // Skip item if any model is missing
            }
            prefabPaths.Add(prefabPath);
        }

        // Create ClothingItem
        ClothingItem clothingItem = ScriptableObject.CreateInstance<ClothingItem>();
        clothingItem.itemName = item.AvatarName;
        clothingItem.isMale = isMale;
        clothingItem.clothingType = clothingType;

        // Setup Addressables for prefabs
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("Addressable Asset Settings not found!");
            return false;
        }

        AddressableAssetGroup group = settings.FindGroup("ClothingItems");
        if (group == null)
        {
            Debug.LogError("ClothingItems group not found in Addressables!");
            return false;
        }

        foreach (string prefabPath in prefabPaths)
        {
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning($"Could not get GUID for prefab: {prefabPath}");
                continue;
            }

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = $"Assets/AddressableAssets/{Path.GetFileName(prefabPath)}";

            clothingItem.prefabs.Add(new AssetReferenceGameObject(guid));
        }

        if (clothingItem.prefabs.Count == 0)
        {
            Debug.LogWarning($"No valid prefabs added for {item.AvatarName} ({gender})");
            return false;
        }

        // Setup icon sprite (don't skip if missing)
        if (!string.IsNullOrEmpty(iconName))
        {
            string iconPath = FindIconPath(iconName);
            if (!string.IsNullOrEmpty(iconPath))
            {
                string iconGuid = AssetDatabase.AssetPathToGUID(iconPath);
                if (!string.IsNullOrEmpty(iconGuid))
                {
                    AddressableAssetEntry iconEntry = settings.CreateOrMoveEntry(iconGuid, group);
                    iconEntry.address = $"Assets/AddressableAssets/{Path.GetFileName(iconPath)}";
                    clothingItem.sprite = new AssetReferenceSprite(iconGuid);
                }
                else
                {
                    Debug.LogWarning($"Could not get GUID for icon: {iconPath}");
                }
            }
            else
            {
                Debug.LogWarning($"Icon not found: {iconName} for {item.AvatarName} ({gender}) - continuing without icon");
            }
        }

        // Calculate disablesClothingType from first prefab
        SetDisablesClothingType(prefabPaths[0], clothingItem);

        // Save asset
        if (!outputFolderMapping.TryGetValue(clothingType, out string outputFolder))
        {
            outputFolder = clothingType.ToString();
        }

        string assetPath = $"Assets/ScriptableObjects/{gender}/{outputFolder}/{SanitizeFileName(clothingItem.itemName)}.asset";
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));

        // Check if asset exists and handle overwrite
        var existing = AssetDatabase.LoadAssetAtPath<ClothingItem>(assetPath);
        if (existing != null)
        {
            if (!OverwriteExisting)
            {
                Debug.Log($"Skipping existing ClothingItem (overwrite disabled): {assetPath}");
                return false;
            }
            else
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }

        AssetDatabase.CreateAsset(clothingItem, assetPath);
        Debug.Log($"Created ClothingItem: {clothingItem.itemName} ({gender}) at {assetPath}");

        return true;
    }

    private static string FindPrefabPath(string prefabName, string gender)
    {
        // Search for prefab in model folders
        string searchPattern = $"{prefabName}.fbx";
        string genderPath = Path.Combine(modelBasePath, gender);

        if (!Directory.Exists(genderPath))
        {
            Debug.LogWarning($"Gender folder not found: {genderPath}");
            return null;
        }

        // Search recursively for the prefab
        string[] foundFiles = Directory.GetFiles(genderPath, searchPattern, SearchOption.AllDirectories);
        
        if (foundFiles.Length > 0)
        {
            return foundFiles[0].Replace("\\", "/");
        }

        return null;
    }

    private static string FindIconPath(string iconName)
    {
        if (!Directory.Exists(clothesIconsPath))
        {
            Debug.LogWarning($"Icons folder not found: {clothesIconsPath}");
            return null;
        }

        // Search for icon with or without extension
        string[] extensions = { ".png", ".jpg", ".jpeg", ".tga" };
        
        foreach (string ext in extensions)
        {
            string iconPath = Path.Combine(clothesIconsPath, iconName + ext);
            if (File.Exists(iconPath))
            {
                return iconPath.Replace("\\", "/");
            }
        }

        // Try without adding extension (in case it's already in the name)
        string directPath = Path.Combine(clothesIconsPath, iconName);
        if (File.Exists(directPath))
        {
            return directPath.Replace("\\", "/");
        }

        return null;
    }

    private static void SetDisablesClothingType(string prefabPath, ClothingItem clothingItem)
    {
        // Load FBX as GameObject
        GameObject fbxObject = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (fbxObject == null)
        {
            return;
        }

        clothingItem.disablesClothingType = new List<ClothingType>();

        // Special case: single-variant shirts disable overwear
        if (clothingItem.clothingType == ClothingType.Shirt && clothingItem.prefabs.Count == 1)
        {
            clothingItem.disablesClothingType.Add(ClothingType.Overwear);
        }

        // Iterate through all SkinnedMeshRenderers in FBX
        foreach (var renderer in fbxObject.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            string rendererName = renderer.name.ToLower().FirstCharacterToUpper();
            if (folderMappings.TryGetValue(rendererName, out var disabledType) &&
                disabledType != clothingItem.clothingType) // Avoid disabling its own type
            {
                if (!clothingItem.disablesClothingType.Contains(disabledType))
                {
                    clothingItem.disablesClothingType.Add(disabledType);
                }
            }

            // Special case: hats with hair materials disable hair
            if (clothingItem.clothingType == ClothingType.Hat)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null && mat.name.ContainsInsensitive("hair"))
                    {
                        if (!clothingItem.disablesClothingType.Contains(ClothingType.Hair))
                        {
                            clothingItem.disablesClothingType.Add(ClothingType.Hair);
                        }
                        break;
                    }
                }
            }
        }
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return "unnamed_item";
        }

        // Replace illegal file name characters with underscore
        string illegalCharsPattern = @"[\/:*?""<>|]";
        string cleanedFileName = Regex.Replace(input, illegalCharsPattern, "_");

        // Remove leading/trailing whitespace
        cleanedFileName = cleanedFileName.Trim();

        return string.IsNullOrEmpty(cleanedFileName) ? "unnamed_item" : cleanedFileName;
    }

    [MenuItem("Tools/Generate Clothing Items New/Overwrite Existing")]
    private static void ToggleOverwriteExisting()
    {
        OverwriteExisting = !OverwriteExisting;
    }

    [MenuItem("Tools/Generate Clothing Items New/Clear All Generated Assets")]
    private static void ClearAllGeneratedAssets()
    {
        if (!EditorUtility.DisplayDialog("Clear All Clothing Assets",
            "This will DELETE all ClothingItem assets in Assets/ScriptableObjects/Male and Assets/ScriptableObjects/Female folders. This action cannot be undone.\n\nAre you sure?",
            "Yes, Delete All", "Cancel"))
        {
            return;
        }

        int deletedCount = 0;

        string[] genders = { "Male", "Female" };
        foreach (string gender in genders)
        {
            string genderPath = $"Assets/ScriptableObjects/{gender}";
            if (!Directory.Exists(genderPath))
            {
                continue;
            }

            string[] assetFiles = Directory.GetFiles(genderPath, "*.asset", SearchOption.AllDirectories);
            foreach (string assetFile in assetFiles)
            {
                string assetPath = assetFile.Replace("\\", "/");
                var asset = AssetDatabase.LoadAssetAtPath<ClothingItem>(assetPath);
                if (asset != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                    deletedCount++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Deleted {deletedCount} ClothingItem assets.");
    }
}
