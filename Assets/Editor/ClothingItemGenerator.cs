using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.AddressableAssets;
using Newtonsoft.Json.Linq;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;

public class ClothingItemGenerator : MonoBehaviour
{
    private static string dataJsFilePath = "Assets/Clothes/ElChicoEevee_files/index_data/data.js";

    private static readonly Dictionary<string, (ClothingType type, string outputFolder)> folderMappings = new Dictionary<string, (ClothingType, string)>
    {
        { "Hair", (ClothingType.Hair, "Hair") },
        { "Outwear", (ClothingType.Overwear, "Overwear") },
        { "Bottom", (ClothingType.Pants, "Pants") },
        { "Hand", (ClothingType.Gloves, "Gloves") },
        { "Sock", (ClothingType.Socks, "Socks") },
        { "Eye", (ClothingType.Eyes, "Eyes") },
        { "Hat", (ClothingType.Hat, "Hats") },
        { "Top", (ClothingType.Shirt, "Shirt") },
        { "Foot", (ClothingType.Shoes, "Shoes") },
        { "Head", (ClothingType.Face, "Face") }
    };

    private static int undefinedNameCounter = -1;

    [MenuItem("Tools/Generate Clothing Items")]
    static void GenerateClothingItems()
    {
        undefinedNameCounter = -1;

        var clothingData = ParseClothingDataFromJs(dataJsFilePath);

        string baseFolderPath = "Assets/Model/Clothing";
        string[] genders = { "Male", "Female" };

        foreach (string gender in genders)
        {
            foreach (var folderMapping in folderMappings)
            {
                string modelFolderName = folderMapping.Key;
                var (clothingType, outputFolder) = folderMapping.Value;
                string typeFolderPath = Path.Combine(baseFolderPath, gender, modelFolderName);

                if (!Directory.Exists(typeFolderPath)) continue;

                foreach (string lodFolder in Directory.GetDirectories(typeFolderPath, "Lv*"))
                {
                    if (lodFolder.Contains("Lv1"))
                        continue;

                    foreach (string modelFolder in Directory.GetDirectories(lodFolder))
                    {
                        string folderName = Path.GetFileName(modelFolder);
                        if (!folderName.StartsWith("pref_lob")) continue;

                        Debug.Log($"Processing folder: {folderName}, Model folder: {modelFolder}");

                        Match match = Regex.Match(folderName, @"\d{5}");

                        if (match.Success)
                        {
                            string numberString = match.Value;
                            int number = int.Parse(numberString);

                            int firstTwoDigits = int.Parse(numberString.Substring(0, 2));
                            bool isMale = firstTwoDigits == 40;

                            int modelId = int.Parse(numberString.Substring(2, 3));

                            Debug.Log($"Model ID: {modelId}, isMale: {isMale}");

                            clothingData.TryGetValue((modelId, isMale, clothingType), out var clothingInfo);

                            /*if (!clothingData.TryGetValue((modelId, isMale, clothingType), out var clothingInfo) ||
                                (isMale != clothingInfo.isMale))
                            {
                                // Log the details for debugging purposes
                                Debug.LogWarning($"Skipping model with ID: {modelId} and ClothingType: {clothingType}." +
                                                 $"\nReason: {(clothingData.ContainsKey((modelId, isMale, clothingType)) ? "Gender mismatch" : "Model ID and ClothingType not found")}" +
                                                 $"\nIsMale: {isMale}, ClothingInfo.isMale: {clothingInfo.isMale}, ClothingInfo.name: {clothingInfo.name}");
                                continue;
                            }*/

                            Debug.Log($"Match found: {clothingInfo.name}, isMale: {clothingInfo.isMale}, type: {clothingType}");

                            ClothingItem clothingItem = ScriptableObject.CreateInstance<ClothingItem>();
                            clothingItem.itemName = clothingInfo.name;
                            clothingItem.isMale = clothingInfo.isMale;
                            clothingItem.clothingType = clothingType;

                            string prefabPath = $"{modelFolder}/{folderName}.fbx";
                            string addressableName = $"Assets/AddressableAssets/{folderName}.fbx"; // Adjust this to your addressable setup

                            if (!File.Exists(prefabPath))
                            {
                                Debug.LogWarning($"Prefab not found: {prefabPath}");
                                continue;
                            }

                            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
                            AddressableAssetGroup group = settings.DefaultGroup;
                            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
                            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
                            entry.address = addressableName;

                            clothingItem.prefab = new AssetReferenceGameObject(guid);

                            string assetPath = $"Assets/ScriptableObjects/{gender}/{outputFolder}/{SanitizeFileName(clothingItem.itemName)}.asset";
                            Debug.Log($"Asset path: {assetPath}");
                            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                            AssetDatabase.CreateAsset(clothingItem, assetPath);

                            Debug.Log($"Created ClothingItem: {clothingItem.itemName}");
                        }
                        else
                        {
                            Debug.LogWarning("No match found");
                            continue;
                        }
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static string SanitizeFileName(string input)
    {
        if (string.IsNullOrEmpty(input) || input.Equals("undefined name"))
        {
            undefinedNameCounter++;
            return $"undefined name {undefinedNameCounter}";
        }

        // Define the regex pattern to match illegal file name characters
        string illegalCharsPattern = @"[\/:*?""<>|]";

        // Replace each illegal character with an underscore
        string cleanedFileName = Regex.Replace(input, illegalCharsPattern, "_");

        // Optionally, you can also remove any leading or trailing whitespace
        cleanedFileName = cleanedFileName.Trim();

        return cleanedFileName;
    }

    static Dictionary<(int modelId, bool isMale, ClothingType clothingType), (string name, bool isMale, ClothingType clothingType)> ParseClothingDataFromJs(string jsFilePath)
    {
        var clothingData = new Dictionary<(int, bool, ClothingType), (string, bool, ClothingType)>();

        string jsonContent = File.ReadAllText(jsFilePath);
        var jsonArray = JArray.Parse(jsonContent);

        foreach (var item in jsonArray)
        {
            string name;
            if (item["Name"] != null)
            {
                name = item["Name"].ToString();
            }
            else
            {
                name = "undefined name";
            }

            foreach (var type in item["type"])
            {
                string typeStr = type.ToString();
                string[] parts = typeStr.Split('_');

                try
                {
                    string clothingTypeStr = parts[1];
                    if (clothingTypeStr.ToLower().Equals("face"))
                    {
                        bool isMale = parts[2].Substring(0, 4) == "male";
                        int modelId = int.Parse(parts[2][parts[2].Length-1].ToString());

                        Debug.Log($"Name: {name}, Type: {clothingTypeStr}, isMale: {isMale}, Model ID: {modelId}");

                        if (Enum.TryParse(clothingTypeStr, true, out ClothingType clothingType))
                        {
                            clothingData[(modelId, isMale, clothingType)] = (name, isMale, clothingType);
                        }
                    }
                    else
                    {
                        bool isMale = parts[2] == "male";
                        int modelId = int.Parse(parts[3]);

                        Debug.Log($"Name: {name}, Type: {clothingTypeStr}, isMale: {isMale}, Model ID: {modelId}");

                        if (Enum.TryParse(clothingTypeStr, true, out ClothingType clothingType))
                        {
                            clothingData[(modelId, isMale, clothingType)] = (name, isMale, clothingType);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Error parsing type: {typeStr}. Exception: {ex.Message}");
                    continue;
                }
            }
        }

        return clothingData;
    }
}
