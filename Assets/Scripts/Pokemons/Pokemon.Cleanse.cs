using Unity.Netcode;

public partial class Pokemon
{
    /// <summary>Removes conditions/debuffs without stripping beneficial status effects.</summary>
    public void CleanseConditions()
    {
        if (!IsServer) return;
        foreach (var effect in statusEffects.AsNativeArray().ToArray())
            if (effect.IsNegativeStatus() || effect.Type == StatusType.Burned || effect.Type == StatusType.Poisoned)
                RemoveStatusEffectRPC(effect);
        foreach (var change in statChanges.AsNativeArray().ToArray())
            if (change.CanBeRemoved && !change.IsBuff) RemoveStatChangeRPC(change);
    }
}
