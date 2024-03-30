using UnityEngine;

[CreateAssetMenu(fileName = "Move", menuName = "Moves/Move", order = 1)]
public class MoveAsset : ScriptableObject
{
    public AvailableMoves move;
    public MoveType moveType;
    public Sprite icon;
    public int uniteEnergyCost;
}

public enum MoveType { MoveA, MoveB, UniteMove, All }