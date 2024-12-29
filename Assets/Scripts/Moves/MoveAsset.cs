using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "Move", menuName = "Create New Move", order = 1)]
public class MoveAsset : ScriptableObject
{
    public AvailableMoves move;
    public MoveType moveType;
    public MoveLabels moveLabel;
    public float cooldown;
    public int uniteEnergyCost;

    [Space]

    public string moveName;
    [TextArea(3, 5)]
    public string description;
    public Sprite icon;
    public AssetReferenceSprite preview;

    [HideInInspector] public bool isUpgraded;
}

public enum MoveLabels { None, Melee, Ranged, Area, Recovery, Debuff, Hinderance, SureHit, Dash, Buff }

public enum MoveType { MoveA, MoveB, UniteMove, All }