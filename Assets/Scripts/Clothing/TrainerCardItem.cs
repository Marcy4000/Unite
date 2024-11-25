using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "New Trainer Card Item", menuName = "Clothing/Trainer Card Item")]
public class TrainerCardItem : ScriptableObject
{
    public string itemName;
    public AssetReferenceSprite itemIcon;
    public AssetReferenceSprite itemSprite;
}
