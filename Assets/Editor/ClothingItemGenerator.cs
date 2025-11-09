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
using Unity.VisualScripting;

public class ClothingItemGenerator : MonoBehaviour
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

    private static int undefinedNameCounter = -1;

	// Aggiunte: preferenza persistente per sovrascrittura e menu toggle
	private const string OverwritePrefKey = "ClothingGenerator_OverwriteExisting";
	private static bool OverwriteExisting
	{
		get => EditorPrefs.GetBool(OverwritePrefKey, true); // default true (sovrascrivi)
		set
		{
			EditorPrefs.SetBool(OverwritePrefKey, value);
			Menu.SetChecked("Tools/Generate Clothing Items/Overwrite Existing", value);
		}
	}

	[MenuItem("Tools/Generate Clothing Items/Overwrite Existing")]
	private static void ToggleOverwriteExisting()
	{
		OverwriteExisting = !OverwriteExisting;
	}

	[InitializeOnLoadMethod]
	private static void InitOverwriteMenuCheck()
	{
		// Assicura che la checkmark rifletta lo stato salvato all'avvio dell'Editor
		Menu.SetChecked("Tools/Generate Clothing Items/Overwrite Existing", OverwriteExisting);
	}

    [MenuItem("Tools/Generate Clothing Items/Generate")]
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

                            int firstTwoDigits = int.Parse(numberString.Substring(0, 2));
                            bool isMale = firstTwoDigits == 40;

                            int modelId = int.Parse(numberString.Substring(2, 3));

                            Debug.Log($"Model ID: {modelId}, isMale: {isMale}");

                            clothingData.TryGetValue((modelId, isMale, clothingType), out var clothingInfo);

                            Debug.Log($"Match found: {clothingInfo}, isMale: {isMale}, type: {clothingType}, model ID: {modelId}");

                            ClothingItem clothingItem = ScriptableObject.CreateInstance<ClothingItem>();
                            clothingItem.itemName = string.IsNullOrEmpty(clothingInfo) ? "undefined name" : clothingInfo;
                            clothingItem.isMale = isMale;
                            clothingItem.clothingType = clothingType;

                            string prefabPath = $"{modelFolder}/{folderName}.fbx";
                            string addressableName = $"Assets/AddressableAssets/{folderName}.fbx"; // Adjust this to your addressable setup

                            if (!File.Exists(prefabPath))
                            {
                                Debug.LogWarning($"Prefab not found: {prefabPath}");
                                continue;
                            }

                            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
                            AddressableAssetGroup group = settings.FindGroup("ClothingItems");
                            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
                            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
                            entry.address = addressableName;

                            if (clothingType == ClothingType.Shirt)
                            {
                                if (folderName.Contains("top_"))
                                {
                                    clothingItem.prefabs.Add(new AssetReferenceGameObject(guid));
                                }
                                else
                                {
                                    SetShirtVariants(clothingItem, folderName, modelFolder);
                                }
                            }
                            else if (clothingType == ClothingType.Socks)
                            {
                                SetSockVariants(clothingItem, folderName, modelFolder, isMale);
                            }
                            else
                            {
                                clothingItem.prefabs.Add(new AssetReferenceGameObject(guid));
                            }

                            SetDisablesClothingType(prefabPath, clothingItem);

                            if (clothingItem.prefabs.Count == 0)
                            {
                                continue;
                            }

                            string genderString = isMale ? "male" : "female";

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

                            if (clothingType == ClothingType.Face)
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

                            string assetPath = $"Assets/ScriptableObjects/{gender}/{outputFolder}/{SanitizeFileName(clothingItem.itemName)}.asset";
                            Debug.Log($"Asset path: {assetPath}");
                            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));

							// Nuova logica: non sovrascrivere se esistente e toggle disattivato
							var existing = AssetDatabase.LoadAssetAtPath<ClothingItem>(assetPath);
							if (existing != null)
							{
								if (!OverwriteExisting)
								{
									Debug.Log($"Skipping existing ClothingItem (overwrite disabled): {assetPath}");
									continue;
								}
								else
								{
									// Cancella l'asset esistente prima di crearne uno nuovo
									AssetDatabase.DeleteAsset(assetPath);
								}
							}

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

    private static void SetShirtVariants(ClothingItem clothingItem, string folderName, string modelFolder)
    {
        if (!folderName.Contains("top1"))
        {
            return;
        }

        string[] variants = { "top1", "top2" };

        clothingItem.prefabs.Clear();

        foreach (string variant in variants)
        {
            string targetFolder = folderName.Replace("top1", variant);

            string newModelFolder = modelFolder.Replace("top1", variant);

            string prefabPath = $"{newModelFolder}/{targetFolder}.fbx";
            string addressableName = $"Assets/AddressableAssets/{targetFolder}.fbx"; // Adjust this to your addressable setup

            if (!File.Exists(prefabPath))
            {
                continue;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetGroup group = settings.FindGroup("ClothingItems");
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = addressableName;

            clothingItem.prefabs.Add(new AssetReferenceGameObject(guid));
        }
    }

    private static void SetSockVariants(ClothingItem clothingItem, string folderName, string modelFolder, bool isMale)
    {
        if (!folderName.Contains("sock1"))
        {
            return;
        }

        string[] variants = isMale ? new string[] { "sock1", "sock2" } : new string[] { "sock1", "sock2", "sock3" };

        clothingItem.prefabs.Clear();

        foreach (string variant in variants)
        {
            string targetFolder = folderName.Replace("sock1", variant);

            string newModelFolder = modelFolder.Replace("sock1", variant);

            string prefabPath = $"{newModelFolder}/{targetFolder}.fbx";
            string addressableName = $"Assets/AddressableAssets/{targetFolder}.fbx"; // Adjust this to your addressable setup
            if (!File.Exists(prefabPath))
            {
                continue;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetGroup group = settings.FindGroup("ClothingItems");
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = addressableName;

            clothingItem.prefabs.Add(new AssetReferenceGameObject(guid));
        }
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

        if (clothingItem.clothingType == ClothingType.Shirt && clothingItem.prefabs.Count == 1)
        {
            clothingItem.disablesClothingType.Add(ClothingType.Overwear);
        }

        // Iterate through all SkinnedMeshRenderers in FBX
        foreach (var renderer in fbxObject.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            string rendererName = renderer.name.ToLower().FirstCharacterToUpper();
            if (folderMappings.TryGetValue(rendererName, out var clothingType) &&
                clothingType.type != clothingItem.clothingType) // Avoid disabling its own type
            {
                clothingItem.disablesClothingType.Add(clothingType.type);
            }

            if (clothingItem.clothingType == ClothingType.Hat)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat.name.ContainsInsensitive("hair"))
                    {
                        clothingItem.disablesClothingType.Add(ClothingType.Hair);
                        break;
                    }
                }
            }
        }
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
                        int modelId = int.Parse(parts[2][parts[2].Length-1].ToString());

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
