using System;
using Unity.Netcode;

public partial class Pokemon
{
    // Server-only, post-resolution events. Item/secondary classification travels
    // with damage so consumers need not identify attacks by their coefficients.
    public event Action<Pokemon, DamageInfo, int> OnDamageApplied;
    public event Action OnOffensiveAction;

    public void NotifyOffensiveAction()
    {
        if (IsServer && CurrentHp > 0) OnOffensiveAction?.Invoke();
    }

    // Idempotent permanent modifiers for passives. Ordinary cleanses still
    // respect CanBeRemoved; replacing/disposing the passive can release its stat.
    [Rpc(SendTo.Server)]
    public void SetPersistentStatRPC(StatChange change, bool active)
    {
        if (change.ID == 0 || change.IsTimed) return;
        for (int i = statChanges.Count - 1; i >= 0; i--)
            if (statChanges[i].ID == change.ID) RemoveStatAt(i);
        if (active) AddStatChange(change);
    }
}
