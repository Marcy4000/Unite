using UnityEngine;

[CreateAssetMenu(fileName = "Held Item", menuName = "Create New Held Item")]
public class HeldItemInfo : ScriptableObject
{
    public string heldItemName;

    [TextArea(3, 10)]
    public string description;

    public AvailableHeldItems heldItemID;

    public Sprite icon;
}
