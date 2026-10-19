using UnityEngine;

public enum MoveUseType { SingleUse, StoredUses }

/// <summary>
/// Sequential reserve recovery and the independent delay between casts.
/// The caller supplies the clock so recovery works while dead and is testable.
/// Cooldowns passed here are already adjusted for CDR.
/// </summary>
public sealed class MoveUseState
{
    public bool IsInitialized { get; private set; }
    public int Count { get; private set; }
    public int Capacity { get; private set; }
    public float RechargeDuration { get; private set; }
    public float NextRecovery { get; private set; }
    private float nextUse;

    public void Tick(float now, int capacity, float rechargeDuration)
    {
        capacity = Mathf.Max(1, capacity);
        rechargeDuration = Mathf.Max(0.01f, rechargeDuration);
        if (!IsInitialized)
        {
            IsInitialized = true;
            Count = Capacity = capacity;
            RechargeDuration = rechargeDuration;
            return;
        }

        Recover(now);
        // Preserve recharge progress when CDR changes instead of resetting it.
        if (Count < Capacity && !Mathf.Approximately(RechargeDuration, rechargeDuration))
            NextRecovery = now + (NextRecovery - now) / RechargeDuration * rechargeDuration;
        RechargeDuration = rechargeDuration;
        Count = Mathf.Clamp(Count + Mathf.Max(0, capacity - Capacity), 0, capacity);
        Capacity = capacity;
        if (Count == Capacity) NextRecovery = 0;
    }

    private void Recover(float now)
    {
        while (Count < Capacity && now >= NextRecovery)
        {
            Count++;
            NextRecovery += RechargeDuration;
        }
    }

    public bool TrySpend(float now, float useInterval)
    {
        if (!IsInitialized) return false;
        Recover(now);
        if (GetUseCooldown(now) > 0 || Count == 0) return false;
        if (Count == Capacity) NextRecovery = now + RechargeDuration;
        Count--;
        nextUse = now + Mathf.Max(0, useInterval);
        return true;
    }

    public float GetRechargeRemaining(float now)
    {
        return Count < Capacity ? Mathf.Max(0, NextRecovery - now) : 0;
    }

    public float GetUseCooldown(float now)
    {
        return Mathf.Max(Mathf.Max(0, nextUse - now), Count == 0 ? GetRechargeRemaining(now) : 0);
    }

    public void ReduceRecharge(float amount, float now)
    {
        if (!IsInitialized || Count == Capacity || amount <= 0) return;
        NextRecovery -= amount;
        Recover(now);
        if (Count == Capacity) NextRecovery = 0;
    }
}
