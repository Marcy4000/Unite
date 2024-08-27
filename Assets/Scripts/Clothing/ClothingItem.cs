using UnityEngine;
using UnityEngine.AddressableAssets;

public enum ClothingType : byte
{
    Hat, Hair, Face, Eyes, Shirt, Overwear, Gloves, Pants, Socks, Shoes
}

[CreateAssetMenu(fileName = "New Clothing Item", menuName = "Clothes")]
public class ClothingItem : ScriptableObject
{
    public string itemName;
    public bool isMale;
    public ClothingType clothingType;

    public AssetReferenceSprite sprite;
    public AssetReferenceGameObject prefab;
}
