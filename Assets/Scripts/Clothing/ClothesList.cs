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
}