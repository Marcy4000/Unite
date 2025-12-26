using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class TrainerCardItemCreator : EditorWindow
{
    private const string BackgroundsPath = "Assets/Sprites/Lobby/TrainerCards/Backgrounds/";
    private const string FramesPath = "Assets/Sprites/Lobby/TrainerCards/Frames/";
    private const string SavePath = "Assets/ScriptableObjects/TrainerCard/";

    [MenuItem("Tools/Trainer Card Item Creator")]
    public static void OpenWindow()
    {
        GetWindow<TrainerCardItemCreator>("Trainer Card Item Creator");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Generate Trainer Card Items"))
        {
            GenerateTrainerCardItems(BackgroundsPath, "Backgrounds");
            GenerateTrainerCardItems(FramesPath, "Frames");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Trainer Card Items created successfully!");
        }
    }

    private static void GenerateTrainerCardItems(string folderPath, string category)
    {
        string categorySavePath = Path.Combine(SavePath, category);
        if (!Directory.Exists(categorySavePath))
        {
            Directory.CreateDirectory(categorySavePath);
        }

        // Load all sprites and icons from the folder
        string iconsPath = Path.Combine(folderPath, "Icons");
        string[] spriteFiles = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly).Where(f => Path.GetFileNameWithoutExtension(f).StartsWith("t_entrycard_")).ToArray();
        string[] iconFiles = Directory.GetFiles(iconsPath, "*.png", SearchOption.TopDirectoryOnly).Where(f => Path.GetFileNameWithoutExtension(f).StartsWith("t_entrycard_") && Path.GetFileNameWithoutExtension(f).Contains("_icon")).ToArray();

        // Group sprites by item name
        var items = new System.Collections.Generic.Dictionary<string, (string cat, string num, System.Collections.Generic.List<string> sprites)>();
        foreach (string spriteFile in spriteFiles)
        {
            string spriteFileName = Path.GetFileNameWithoutExtension(spriteFile);
            string baseName = RemoveHexSuffix(spriteFileName);
            var (cat, num) = ParseCategoryAndNumber(baseName);
            if (cat == null || num == null) continue;
            string itemName = cat + num;
            if (!items.ContainsKey(itemName))
            {
                items[itemName] = (cat, num, new System.Collections.Generic.List<string>());
            }
            items[itemName].sprites.Add(spriteFile);
        }

        // Process each unique item
        foreach (var kvp in items)
        {
            string itemName = kvp.Key;
            var (cat, num, sprites) = kvp.Value;
            string expectedIconBase = $"t_entrycard_{cat}_icon{num}";
            var matchingIcons = iconFiles.Where(icon => {
                string name = Path.GetFileNameWithoutExtension(icon);
                return name.StartsWith(expectedIconBase) && (name.Length == expectedIconBase.Length || name[expectedIconBase.Length] == '_');
            }).ToArray();
            if (sprites.Any() && matchingIcons.Any())
            {
                string bestSprite = SelectBestSprite(sprites.ToArray());
                string bestIcon = SelectBestIcon(matchingIcons);
                // Create the ScriptableObject
                TrainerCardItem newItem = ScriptableObject.CreateInstance<TrainerCardItem>();
                newItem.itemName = itemName;
                newItem.itemSprite = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID(bestSprite));
                newItem.itemIcon = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID(bestIcon));
                // Save the ScriptableObject
                string assetPath = Path.Combine(categorySavePath, $"{itemName}.asset");
                AssetDatabase.CreateAsset(newItem, assetPath);
                Debug.Log($"Created TrainerCardItem: {itemName} at {assetPath}");
            }
            else
            {
                Debug.LogWarning($"Missing assets for item: {itemName}. Sprites: {sprites.Count}, Icons: {matchingIcons.Length}");
            }
        }
    }

    private static string RemoveHexSuffix(string name)
    {
        var parts = name.Split('_');
        if (parts.Length > 1 && parts.Last().Length == 16 && parts.Last().All(c => "0123456789abcdefABCDEF".Contains(c)))
        {
            return string.Join("_", parts.Take(parts.Length - 1));
        }
        return name;
    }

    private static (string cat, string num) ParseCategoryAndNumber(string baseName)
    {
        const string prefix = "t_entrycard_";
        if (!baseName.StartsWith(prefix)) return (null, null);
        string rest = baseName.Substring(prefix.Length);
        int i = 0;
        string cat = "";
        while (i < rest.Length && !char.IsDigit(rest[i]))
        {
            cat += rest[i];
            i++;
        }
        string num = rest.Substring(i);
        if (string.IsNullOrEmpty(cat) || string.IsNullOrEmpty(num)) return (null, null);
        return (cat, num);
    }

    private static string SelectBestIcon(string[] icons)
    {
        // Prefer the one with hex suffix
        var withHex = icons.FirstOrDefault(icon =>
        {
            string name = Path.GetFileNameWithoutExtension(icon);
            var parts = name.Split('_');
            return parts.Length > 1 && parts.Last().Length == 16 && parts.Last().All(c => "0123456789abcdefABCDEF".Contains(c));
        });
        return withHex ?? icons[0];
    }

    private static string SelectBestSprite(string[] sprites)
    {
        // Prefer the one with hex suffix
        var withHex = sprites.FirstOrDefault(sprite =>
        {
            string name = Path.GetFileNameWithoutExtension(sprite);
            var parts = name.Split('_');
            return parts.Length > 1 && parts.Last().Length == 16 && parts.Last().All(c => "0123456789abcdefABCDEF".Contains(c));
        });
        return withHex ?? sprites[0];
    }
}
