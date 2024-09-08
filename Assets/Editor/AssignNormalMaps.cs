using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class AssignNormalMaps : EditorWindow
{
    // Root folder of your clothing assets
    private const string RootFolder = "Assets/Model/Clothing";
    private const string LodLevel = "Lv3"; // Only care about LOD 3

    [MenuItem("Tools/Assign Normal Maps for URP")]
    public static void AssignNormalMapsToMaterials()
    {
        string[] genderFolders = { "Male", "Female" };

        // Iterate over both gender folders
        foreach (var genderFolder in genderFolders)
        {
            string genderPath = Path.Combine(RootFolder, genderFolder);
            if (!Directory.Exists(genderPath))
            {
                Debug.LogError($"Folder not found: {genderPath}");
                continue;
            }

            // Go through each clothing type folder within the gender folder
            foreach (var folderMapping in folderMappings)
            {
                string typeFolderPath = Path.Combine(genderPath, folderMapping.Key, LodLevel);
                if (!Directory.Exists(typeFolderPath))
                {
                    Debug.LogWarning($"Folder not found: {typeFolderPath}");
                    continue;
                }

                // Process all models in this folder
                ProcessModelsInFolder(typeFolderPath);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Normal maps assignment for URP completed.");
    }

    private static void ProcessModelsInFolder(string folderPath)
    {
        string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });

        foreach (var guid in modelGuids)
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            if (model == null) continue;

            // Find all materials used by the model
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null) continue;
                    AssignNormalMapToMaterial(material, folderPath);
                }
            }
        }
    }

    private static void AssignNormalMapToMaterial(Material material, string folderPath)
    {
        string materialName = material.name;
        string normalMapName = materialName.Replace("mat_", "t_nm_");

        // Find the normal map file that matches the material name
        string[] normalMapGuids = AssetDatabase.FindAssets(normalMapName, new[] { folderPath });

        if (normalMapGuids.Length > 0)
        {
            string normalMapPath = AssetDatabase.GUIDToAssetPath(normalMapGuids[0]);
            Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(normalMapPath);

            if (normalMap != null)
            {
                // Assign the normal map to the URP material property
                material.SetTexture("_BumpMap", normalMap);
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_NORMALMAP", 1.0f);
                EditorUtility.SetDirty(material);
                Debug.Log($"Assigned normal map '{normalMapName}' to material '{material.name}' in '{folderPath}'.");
            }
        }
        else
        {
            Debug.LogWarning($"Normal map not found for material '{material.name}' in '{folderPath}'.");
        }
    }

    // Folder mapping for different clothing types
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
}
