using UnityEngine;

[CreateAssetMenu(fileName = "Move", menuName = "Create New Move", order = 1)]
public class MoveAsset : ScriptableObject
{
    public AvailableMoves move;
    public MoveType moveType;
    public MoveLabels moveLabel;
    public Sprite icon;
    [HideInInspector] public bool isUpgraded;
    public int uniteEnergyCost;
}

public enum MoveLabels { None, Melee, Ranged, Area, Recovery, Debuff, Hinderance, SureHit, Dash, Buff }

public enum MoveType { MoveA, MoveB, UniteMove, All }