using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ClothingTypeList
{
    public ClothingType clothingType;
    public List<ClothingItem> clothingItems;
}

public class ClothesList : MonoBehaviour
{
    public static ClothesList Instance;

    [SerializeField] private List<ClothingTypeList> maleClothes;
    [SerializeField] private List<ClothingTypeList> femaleClothes;

    private void Awake()
    {
        Instance = this;
    }

    public ClothingItem GetClothingItem(ClothingType clothingType, int index, bool isMale)
    {
        List<ClothingTypeList> clothesList = isMale ? maleClothes : femaleClothes;

        foreach (var item in clothesList)
        {
            if (item.clothingType == clothingType)
            {
                return item.clothingItems[index];
            }
        }

        return null;
    }

    public byte GetClothingIndex(ClothingType clothingType, ClothingItem item, bool isMale)
    {
        List<ClothingTypeList> clothesList = isMale ? maleClothes : femaleClothes;

        foreach (var list in clothesList)
        {
            if (list.clothingType == clothingType)
            {
                return (byte)list.clothingItems.IndexOf(item);
            }
        }

        return 0;
    }

    public Dictionary<ClothingType, ClothingItem> GetSelectedPlayerClothes(PlayerClothesInfo playerClothesInfo)
    {
        Dictionary<ClothingType, ClothingItem> selectedClothes = new Dictionary<ClothingType, ClothingItem>
        {
            { ClothingType.Hat, GetClothingItem(ClothingType.Hat, playerClothesInfo.Hat, playerClothesInfo.IsMale) },
            { ClothingType.Hair, GetClothingItem(ClothingType.Hair, playerClothesInfo.Hair, playerClothesInfo.IsMale) },
            { ClothingType.Face, GetClothingItem(ClothingType.Face, playerClothesInfo.Face, playerClothesInfo.IsMale) },
            { ClothingType.Eyes, GetClothingItem(ClothingType.Eyes, playerClothesInfo.Eyes, playerClothesInfo.IsMale) },
            { ClothingType.Shirt, GetClothingItem(ClothingType.Shirt, playerClothesInfo.Shirt, playerClothesInfo.IsMale) },
            { ClothingType.Overwear, GetClothingItem(ClothingType.Overwear, playerClothesInfo.Overwear, playerClothesInfo.IsMale) },
            { ClothingType.Gloves, GetClothingItem(ClothingType.Gloves, playerClothesInfo.Gloves, playerClothesInfo.IsMale) },
            { ClothingType.Pants, GetClothingItem(ClothingType.Pants, playerClothesInfo.Pants, playerClothesInfo.IsMale) },
            { ClothingType.Socks, GetClothingItem(ClothingType.Socks, playerClothesInfo.Socks, playerClothesInfo.IsMale) },
            { ClothingType.Shoes, GetClothingItem(ClothingType.Shoes, playerClothesInfo.Shoes, playerClothesInfo.IsMale) }
        };

        return selectedClothes;
    }

    public List<ClothingItem> GetAvailableClothesOfType(ClothingType clothingType, bool isMale)
    {
        List<ClothingTypeList> clothes = isMale ? maleClothes : femaleClothes;

        foreach (var item in clothes)
        {
            if (item.clothingType == clothingType)
            {
                return item.clothingItems;
            }
        }

        return null;
    }

    //Code to print the clothing items to a JSON file for the uniteapi webscraper
    /*[Serializable]
    private class ClothesTypeData
    {
        public string type;
        public List<string> items = new List<string>();
    }

    [Serializable]
    private class ClothesData
    {
        public List<ClothesTypeData> maleClothes = new List<ClothesTypeData>();
        public List<ClothesTypeData> femaleClothes = new List<ClothesTypeData>();
    }

    private void PrintClothesImgNamesToJSON()
    {
        var clothesData = new ClothesData();

        // Process male clothes
        foreach (var clothingTypeList in maleClothes)
        {
            var typeData = new ClothesTypeData
            {
                type = clothingTypeList.clothingType.ToString()
            };

            foreach (var clothingItem in clothingTypeList.clothingItems)
            {
                string fileName = clothingItem.sprite.AssetGUID;
                string filePath = UnityEditor.AssetDatabase.GUIDToAssetPath(fileName);
                string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filePath);
                typeData.items.Add(string.IsNullOrEmpty(fileNameWithoutExtension) ? "null" : fileNameWithoutExtension);
            }

            clothesData.maleClothes.Add(typeData);
        }

        // Process female clothes
        foreach (var clothingTypeList in femaleClothes)
        {
            var typeData = new ClothesTypeData
            {
                type = clothingTypeList.clothingType.ToString()
            };

            foreach (var clothingItem in clothingTypeList.clothingItems)
            {
                string fileName = clothingItem.sprite.AssetGUID;
                string filePath = UnityEditor.AssetDatabase.GUIDToAssetPath(fileName);
                string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filePath);
                typeData.items.Add(string.IsNullOrEmpty(fileNameWithoutExtension) ? "null" : fileNameWithoutExtension);
            }

            clothesData.femaleClothes.Add(typeData);
        }

        string jsonString = JsonUtility.ToJson(clothesData, true);
        Debug.Log($"Clothes JSON:\n{jsonString}");

        string path = Application.dataPath + "/ClothesData.json";
        System.IO.File.WriteAllText(path, jsonString);
        Debug.Log($"Clothes data saved to {path}");
    }*/

}