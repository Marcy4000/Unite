using UnityEngine;

/// <summary>
/// Personal presentation for discrete boosted-attack cycles. Charge includes the
/// ready slot: 0 = empty, 1 = white, 2 = white/yellow (not white/white/yellow).
/// Timed/special HP-bar mechanics continue to use BasicAttackBase directly.
/// </summary>
public abstract class ChargedBasicAttackBase : BasicAttackBase
{
    protected abstract int AttackCharge { get; }
    protected virtual int ReadyCharge => 2;
    protected virtual bool ShowAttackCounter => true;
    protected virtual int StoredBoostedCharges => 0;

    private HPBar counterBar;
    private BoostedAttackCounterModule attackCounter;

    public override void Initialize(PlayerManager manager)
    {
        RemoveAttackCounter();
        base.Initialize(manager);
        UpdateAttackCounter();
    }

    public override void Update()
    {
        UpdateAttackCounter();
    }

    private void UpdateAttackCounter()
    {
        if (playerManager == null) return;
        // Attack factories initialize only the owner. Ownership is meaningful
        // once spawned; reject accidentally attached remote attacks as well.
        if (playerManager.IsSpawned && !playerManager.IsOwner)
        {
            RemoveAttackCounter();
            return;
        }
        HPBar hpBar = playerManager.HPBar;
        if (counterBar != hpBar)
        {
            RemoveAttackCounter();
            counterBar = hpBar;
        }
        if (counterBar == null) return;
        int threshold = Mathf.Max(1, ReadyCharge);
        if (attackCounter == null) attackCounter = counterBar.GetOrCreateBoostedAttackCounter(threshold - 1);
        if (attackCounter == null) return;
        attackCounter.SetVisibility(ShowAttackCounter);
        if (playerManager.Pokemon == null || playerManager.Pokemon.CurrentHp <= 0)
        {
            attackCounter.ResetCounter();
            return;
        }

        // Read actual combat state, never advance UI merely because Perform was
        // attempted. The yellow icon replaces the last charge rather than adding one.
        if (StoredBoostedCharges > 0)
        {
            attackCounter.SetChargeCounts(0, StoredBoostedCharges);
            return;
        }
        int charge = Mathf.Clamp(AttackCharge, 0, threshold);
        bool ready = charge == threshold;
        attackCounter.SetState(ready ? charge - 1 : charge, ready);
    }

    public override void ResetAttack()
    {
        if (attackCounter != null) attackCounter.ResetCounter();
    }

    public override void Dispose()
    {
        RemoveAttackCounter();
        playerManager = null;
    }

    private void RemoveAttackCounter()
    {
        if (counterBar != null && attackCounter != null) counterBar.RemoveModule(attackCounter);
        attackCounter = null;
        counterBar = null;
    }
}
