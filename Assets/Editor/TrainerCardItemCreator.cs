using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class TrainerCardItemCreator : EditorWindow
{
    private const string BackgroundsPath = "Assets/Sprites/Lobby/TrainerCards/Backgrounds/";
    private const string FramesPath = "Assets/Sprites/Lobby/TrainerCards/Frames";
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
        string iconsPath = Path.Combine(folderPath, "icons");
        string[] spriteFiles = Directory.GetFiles(folderPath, "t_entrycard_*.png", SearchOption.TopDirectoryOnly);
        string[] iconFiles = Directory.GetFiles(iconsPath, "t_entrycard_*_icon*.png", SearchOption.TopDirectoryOnly);

        foreach (string spriteFile in spriteFiles)
        {
            string spriteFileName = Path.GetFileNameWithoutExtension(spriteFile);

            // Match the corresponding icon by reconstructing its name
            string expectedIconName = ReconstructIconName(spriteFileName);
            string iconFile = Array.Find(iconFiles, icon => Path.GetFileNameWithoutExtension(icon).Equals(expectedIconName));

            if (iconFile != null)
            {
                string itemName = spriteFileName.Replace("t_entrycard_", "");

                // Create the ScriptableObject
                TrainerCardItem newItem = ScriptableObject.CreateInstance<TrainerCardItem>();
                newItem.itemName = itemName;
                newItem.itemSprite = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID(spriteFile));
                newItem.itemIcon = new AssetReferenceSprite(AssetDatabase.AssetPathToGUID(iconFile));

                // Save the ScriptableObject
                string assetPath = Path.Combine(categorySavePath, $"{spriteFileName}.asset");
                AssetDatabase.CreateAsset(newItem, assetPath);

                Debug.Log($"Created TrainerCardItem: {itemName} at {assetPath}");
            }
            else
            {
                Debug.LogWarning($"No matching icon found for sprite: {spriteFileName}. Expected icon name: {expectedIconName}");
            }
        }
    }

    private static string ReconstructIconName(string spriteFileName)
    {
        // Reconstruct icon name by moving "_icon" before the numerical identifier
        if (spriteFileName.Contains("bg"))
        {
            return spriteFileName.Replace("bg", "bg_icon");
        }
        else if (spriteFileName.Contains("frame"))
        {
            return spriteFileName.Replace("frame", "frame_icon");
        }
        return spriteFileName; // Fallback in case the structure doesn't match
    }
}
