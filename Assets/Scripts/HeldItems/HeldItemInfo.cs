using UnityEngine;

public enum HeldItemCategory : byte { Attack, Defense, Support, Utility }

[CreateAssetMenu(fileName = "Held Item", menuName = "Create New Held Item")]
public class HeldItemInfo : ScriptableObject
{
    public string heldItemName;

    [TextArea(3, 10)]
    [SerializeField]
    private string description;

    public string Description
    {
        get
        {
            string desc = description ?? string.Empty;
            foreach (HeldItemStatBoost boost in StatBoosts)
            {
                if (boost == null) continue;
                desc += $"\n{boost.AffectedStat} +{boost.BoostAmount}{(boost.IsPercentage ? "%" : "")}";
            }
            return desc;
        }
    }

    [Space]
    public HeldItemCategory heldItemCategory;
    public DamageType damageType;

    public AvailableHeldItems heldItemID;

    public Sprite icon;

    public HeldItemStatBoost[] statBoosts = System.Array.Empty<HeldItemStatBoost>();
    // Empty/legacy assets can omit the serialized list. An unequipped slot is
    // valid and must not abort player or character-select initialization.
    public HeldItemStatBoost[] StatBoosts => statBoosts ?? System.Array.Empty<HeldItemStatBoost>();
}

[System.Serializable]
public class HeldItemStatBoost
{
    public Stat AffectedStat;
    public short BoostAmount;
    public bool IsPercentage;
}
