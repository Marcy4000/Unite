using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class ClothingItemUpdater : MonoBehaviour
{
    private static string dataJsFilePath = "Assets/Clothes/ElChicoEevee_files/index_data/data.js";
    private static string clothesIconsPath = "Assets/Clothes/ElChicoEevee_files/index_data";

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
        { "Head", (ClothingType.Face, "Face") },
        { "Backpack", (ClothingType.Backpack, "Backpack")   }
    };

    [ContextMenu("Update Clothing Items")]
    public void UpdateClothingItems()
    {
        var clothingData = ParseClothingDataFromJs(dataJsFilePath);

        string baseFolderPath = "Assets/ScriptableObjects";
        string[] genders = { "Male", "Female" };

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        AddressableAssetGroup group = settings.FindGroup("ClothingItems");

        foreach (string gender in genders)
        {
            foreach (var folderMapping in folderMappings)
            {
                string modelFolderName = folderMapping.Value.type.ToString();
                string typeFolderPath = Path.Combine(baseFolderPath, gender, modelFolderName);

                if (!Directory.Exists(typeFolderPath)) continue;

                // load all clothingitem scriptable objects
                string[] clothingItemGuids = AssetDatabase.FindAssets("t:ClothingItem", new[] { typeFolderPath });

                foreach (var guid in clothingItemGuids)
                {
                    string clothingItemPath = AssetDatabase.GUIDToAssetPath(guid);
                    ClothingItem clothingItem = AssetDatabase.LoadAssetAtPath<ClothingItem>(clothingItemPath);

                    string genderString = clothingItem.isMale ? "male" : "female";
                    int modelId = GetModelId(clothingItem);

                    if (clothingData.TryGetValue((modelId, clothingItem.isMale, clothingItem.clothingType), out var clothingName))
                    {
                        clothingItem.itemName = string.IsNullOrEmpty(clothingName) ? "undefined name" : clothingName;

                        string folderMappingKey = folderMapping.Key;

                        if (folderMappingKey.Equals("Outwear"))
                        {
                            folderMappingKey = "Outerwear";
                        }
                        else if (folderMappingKey.Equals("Sock"))
                        {
                            folderMappingKey = "Socks";
                        }
                        else if (folderMappingKey.Equals("Backpack"))
                        {
                            folderMappingKey = "Bag";
                        }

                        string iconPath = $"t_{folderMappingKey}_{genderString}_{modelId}_";

                        if (clothingItem.clothingType == ClothingType.Face)
                        {
                            iconPath = $"t_face_{genderString}{modelId}_";
                        }

                        foreach (string file in Directory.GetFiles(clothesIconsPath))
                        {
                            if (Path.GetFileName(file).StartsWith(iconPath))
                            {
                                string guidIcon = AssetDatabase.AssetPathToGUID(file);
                                AddressableAssetEntry entryIcon = settings.CreateOrMoveEntry(guidIcon, group);
                                entryIcon.address = $"Assets/AddressableAssets/{Path.GetFileName(file)}";

                                clothingItem.sprite = new AssetReferenceSprite(guidIcon);
                                break;
                            }
                        }

                        EditorUtility.SetDirty(clothingItem);

                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private int GetModelId(ClothingItem clothingItem)
    {
        Match match = Regex.Match(clothingItem.prefabs[0].Asset.name, @"\d{5}");

        if (match.Success)
        {
            string numberString = match.Value;

            int firstTwoDigits = int.Parse(numberString.Substring(0, 2));
            bool isMale = firstTwoDigits == 40;

            int modelId = int.Parse(numberString.Substring(2, 3));

            return modelId;
        }
        else
        {
            return -1;
        }
    }

    static Dictionary<(int modelId, bool isMale, ClothingType clothingType), string> ParseClothingDataFromJs(string jsFilePath)
    {
        var clothingData = new Dictionary<(int, bool, ClothingType), string>();

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
                        int modelId = int.Parse(parts[2][parts[2].Length - 1].ToString());

                        Debug.Log($"Name: {name}, Type: {clothingTypeStr}, isMale: {isMale}, Model ID: {modelId}");

                        if (folderMappings.ContainsKey(clothingTypeStr))
                        {
                            clothingData[(modelId, isMale, folderMappings[clothingTypeStr].type)] = name;
                        }
                    }
                    else
                    {
                        bool isMale = parts[2] == "male";
                        int modelId = int.Parse(parts[3]);

                        Debug.Log($"Name: {name}, Type: {clothingTypeStr}, isMale: {isMale}, Model ID: {modelId}");

                        var clothingTypeStrLower = clothingTypeStr.ToLower();

                        if (clothingTypeStrLower.Equals("outerwear"))
                        {
                            clothingTypeStr = "Outwear";
                        }
                        else if (clothingTypeStrLower.Equals("socks"))
                        {
                            clothingTypeStr = "Sock";
                        }
                        else if (clothingTypeStrLower.Equals("bag"))
                        {
                            clothingTypeStr = "Backpack";
                        }

                        if (folderMappings.ContainsKey(clothingTypeStr))
                        {
                            clothingData[(modelId, isMale, folderMappings[clothingTypeStr].type)] = name;
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
