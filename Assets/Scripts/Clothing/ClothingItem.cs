using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

public enum ClothingType : byte
{
    Hat, Hair, Face, Eyes, Shirt, Overwear, Gloves, Pants, Socks, Shoes, Backpack
}

[CreateAssetMenu(fileName = "New Clothing Item", menuName = "Clothes")]
public class ClothingItem : ScriptableObject
{
    public string itemName;
    public bool isMale;
    public ClothingType clothingType;

    public AssetReferenceSprite sprite;
    public List<AssetReferenceGameObject> prefabs = new List<AssetReferenceGameObject>();

    public List<ClothingType> disablesClothingType = new List<ClothingType>();

    public int sockTypeToUse = 0; // only used for pants
}
