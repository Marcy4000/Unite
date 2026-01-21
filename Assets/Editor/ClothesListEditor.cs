using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ClothesList))]
public class ClothesListEditor : Editor
{
    private static readonly string maleClothesPath = "Assets/ScriptableObjects/Male";
    private static readonly string femaleClothesPath = "Assets/ScriptableObjects/Female";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ClothesList clothesList = (ClothesList)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Auto Fill Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Auto Fill Clothes Lists", GUILayout.Height(30)))
        {
            AutoFillClothesLists(clothesList);
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Clear All Clothes Lists", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Clear Clothes Lists",
                "This will clear all male and female clothes lists. Are you sure?",
                "Yes", "Cancel"))
            {
                ClearClothesLists(clothesList);
            }
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("Auto Fill will scan Assets/ScriptableObjects/Male and Female folders and organize all ClothingItem assets by type.", MessageType.Info);
    }

    private void AutoFillClothesLists(ClothesList clothesList)
    {
        EditorUtility.DisplayProgressBar("Auto Filling Clothes Lists", "Loading male clothes...", 0f);

        try
        {
            // Load male clothes
            Dictionary<ClothingType, List<ClothingItem>> maleClothesDict = LoadClothesFromFolder(maleClothesPath, true);
            
            EditorUtility.DisplayProgressBar("Auto Filling Clothes Lists", "Loading female clothes...", 0.5f);

            // Load female clothes
            Dictionary<ClothingType, List<ClothingItem>> femaleClothesDict = LoadClothesFromFolder(femaleClothesPath, false);

            EditorUtility.DisplayProgressBar("Auto Filling Clothes Lists", "Organizing lists...", 0.75f);

            // Convert dictionaries to lists using SerializedObject for proper undo support
            SerializedObject serializedObject = new SerializedObject(clothesList);

            // Fill male clothes
            SerializedProperty maleClothesProperty = serializedObject.FindProperty("maleClothes");
            FillClothesProperty(maleClothesProperty, maleClothesDict);

            // Fill female clothes
            SerializedProperty femaleClothesProperty = serializedObject.FindProperty("femaleClothes");
            FillClothesProperty(femaleClothesProperty, femaleClothesDict);

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(clothesList);
            AssetDatabase.SaveAssets();

            int maleCount = maleClothesDict.Values.Sum(list => list.Count);
            int femaleCount = femaleClothesDict.Values.Sum(list => list.Count);

            Debug.Log($"Auto Fill Complete! Loaded {maleCount} male items and {femaleCount} female items across {maleClothesDict.Count} clothing types.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private Dictionary<ClothingType, List<ClothingItem>> LoadClothesFromFolder(string folderPath, bool isMale)
    {
        Dictionary<ClothingType, List<ClothingItem>> clothesDict = new Dictionary<ClothingType, List<ClothingItem>>();

        // Initialize all clothing types with empty lists
        foreach (ClothingType type in System.Enum.GetValues(typeof(ClothingType)))
        {
            clothesDict[type] = new List<ClothingItem>();
        }

        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"Folder not found: {folderPath}");
            return clothesDict;
        }

        // Find all ClothingItem assets recursively
        string[] assetFiles = Directory.GetFiles(folderPath, "*.asset", SearchOption.AllDirectories);

        foreach (string assetFile in assetFiles)
        {
            string assetPath = assetFile.Replace("\\", "/");
            ClothingItem item = AssetDatabase.LoadAssetAtPath<ClothingItem>(assetPath);

            if (item != null && item.isMale == isMale)
            {
                if (clothesDict.ContainsKey(item.clothingType))
                {
                    clothesDict[item.clothingType].Add(item);
                }
                else
                {
                    Debug.LogWarning($"Unknown clothing type {item.clothingType} for item {item.itemName}");
                }
            }
        }

        // Sort items by name within each type for consistency
        foreach (var kvp in clothesDict)
        {
            kvp.Value.Sort((a, b) => string.Compare(a.itemName, b.itemName, System.StringComparison.Ordinal));
        }

        return clothesDict;
    }

    private void FillClothesProperty(SerializedProperty clothesProperty, Dictionary<ClothingType, List<ClothingItem>> clothesDict)
    {
        clothesProperty.ClearArray();

        // Add each clothing type that has items
        foreach (var kvp in clothesDict.OrderBy(x => x.Key))
        {
            if (kvp.Value.Count == 0)
                continue;

            int index = clothesProperty.arraySize;
            clothesProperty.InsertArrayElementAtIndex(index);
            SerializedProperty typeListProperty = clothesProperty.GetArrayElementAtIndex(index);

            SerializedProperty typeProperty = typeListProperty.FindPropertyRelative("clothingType");
            typeProperty.enumValueIndex = (int)kvp.Key;

            SerializedProperty itemsProperty = typeListProperty.FindPropertyRelative("clothingItems");
            itemsProperty.ClearArray();

            for (int i = 0; i < kvp.Value.Count; i++)
            {
                itemsProperty.InsertArrayElementAtIndex(i);
                SerializedProperty itemProperty = itemsProperty.GetArrayElementAtIndex(i);
                itemProperty.objectReferenceValue = kvp.Value[i];
            }
        }
    }

    private void ClearClothesLists(ClothesList clothesList)
    {
        SerializedObject serializedObject = new SerializedObject(clothesList);
        
        SerializedProperty maleClothesProperty = serializedObject.FindProperty("maleClothes");
        maleClothesProperty.ClearArray();

        SerializedProperty femaleClothesProperty = serializedObject.FindProperty("femaleClothes");
        femaleClothesProperty.ClearArray();

        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(clothesList);
        AssetDatabase.SaveAssets();

        Debug.Log("Clothes lists cleared.");
    }
}
