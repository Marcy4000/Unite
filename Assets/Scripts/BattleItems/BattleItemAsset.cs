using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Battle item", menuName = "New Battle item", order = 1)]
public class BattleItemAsset : ScriptableObject
{
    public AvailableBattleItems battleItemType;
    public string itemName;

    [TextArea]
    public string description;

    public Sprite icon;
}
