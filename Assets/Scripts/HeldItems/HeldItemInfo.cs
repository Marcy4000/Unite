using UnityEngine;

public enum HeldItemCategory : byte { Attack, Defense, Support, Utility }

[CreateAssetMenu(fileName = "Held Item", menuName = "Create New Held Item")]
public class HeldItemInfo : ScriptableObject
{
    public string heldItemName;

    [TextArea(3, 10)]
    public string description;

    [Space]
    public HeldItemCategory heldItemCategory;
    public DamageType damageType;

    public AvailableHeldItems heldItemID;

    public Sprite icon;

    public HeldItemStatBoost[] statBoosts;
}

[System.Serializable]
public class HeldItemStatBoost
{
    public Stat AffectedStat;
    public short BoostAmount;
    public bool IsPercentage;
}