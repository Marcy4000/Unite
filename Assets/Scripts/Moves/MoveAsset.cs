using UnityEngine;
using UnityEngine.Serialization;
using Unite.Updates;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "Move", menuName = "Create New Move", order = 1)]
public class MoveAsset : ScriptableObject
{
    public AvailableMoves move;
    public MoveType moveType;
    public MoveLabels moveLabel;
    [SerializeField, HideInInspector] private string balanceId;
    public string BalanceId => balanceId;
    [FormerlySerializedAs("authoredCooldown")] public float cooldown;
    [FormerlySerializedAs("authoredUniteEnergyCost")] public int uniteEnergyCost;
    public float EffectiveCooldown => BalanceRuntime.Move(balanceId) is MoveBalance patch && patch.overrideCooldown
        ? (isUpgraded ? patch.upgradedCooldown : patch.cooldown) : cooldown;
    public int EffectiveUniteEnergyCost => BalanceRuntime.Move(balanceId)?.uniteEnergyCost ?? uniteEnergyCost;

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