using System;
using Unity.Netcode;

public partial class Pokemon
{
    // Server-only, post-resolution events. Item/secondary classification travels
    // with damage so consumers need not identify attacks by their coefficients.
    public event Action<Pokemon, DamageInfo, int> OnDamageApplied;
    public event Action OnOffensiveAction;
    // Server-only, after mitigation but before shield absorption. Redirected
    // damage bypasses this hook to prevent mutually linked targets recursing.
    public event Func<DamageInfo, int, int> ModifyIncomingDamage;
    public event Func<int, int> ModifyHealingReceived;
    public event Action<DamageInfo, int> OnDamageReceived;

    private int FilterIncomingDamage(DamageInfo damage, int amount)
    {
        if (damage.source != DamageSource.Redirected && ModifyIncomingDamage != null)
            foreach (Func<DamageInfo, int, int> filter in ModifyIncomingDamage.GetInvocationList()) amount = UnityEngine.Mathf.Max(0, filter(damage, amount));
        return amount;
    }

    private int FilterHealingReceived(int amount)
    {
        if (ModifyHealingReceived != null)
            foreach (Func<int, int> filter in ModifyHealingReceived.GetInvocationList()) amount = UnityEngine.Mathf.Max(0, filter(amount));
        return amount;
    }

    // Health costs are nonlethal true HP loss, not hostile hits: no shields,
    // item procs, Natural Cure, or reflection. The owning move counts its own
    // Curse event exactly once after this succeeds.
    public int PayHealthCost(int amount)
    {
        if (!IsServer || CurrentHp <= 1 || amount <= 0) return 0;
        int paid = UnityEngine.Mathf.Min(amount, CurrentHp - 1);
        currentHp.Value -= paid;
        ClientDamageRPC(paid, new DamageInfo(NetworkObjectId, 0, 0, (short)UnityEngine.Mathf.Min(short.MaxValue, paid), DamageType.True)
            { source = DamageSource.Secondary, suppressOnHitEffects = true }, true);
        return paid;
    }

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
