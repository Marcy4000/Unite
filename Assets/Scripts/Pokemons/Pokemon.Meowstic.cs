using System;
using System.Collections.Generic;
using UnityEngine;

public partial class Pokemon
{
    // Server-side interception happens before a hindrance can interrupt a cast.
    // Returning null rejects a stat change; reflected changes use a bounded context.
    public event Func<StatChange, StatChange?> BeforeStatChange;
    public event Func<StatusEffect, bool> BeforeStatusEffect;
    public event Action<DamageInfo, int> OnResolvedDamage;
    private readonly HashSet<object> wonderRoomSources = new HashSet<object>();

    private bool InterceptMeowsticStat(ref StatChange change)
    {
        if (BeforeStatChange == null) return true;
        foreach (Func<StatChange, StatChange?> filter in BeforeStatChange.GetInvocationList())
        {
            StatChange? result = filter(change);
            if (!result.HasValue) return false;
            change = result.Value;
        }
        return true;
    }

    private bool InterceptMeowsticStatus(StatusEffect effect)
    {
        if (BeforeStatusEffect != null)
            foreach (Func<StatusEffect, bool> filter in BeforeStatusEffect.GetInvocationList())
                if (!filter(effect)) return false;
        return true;
    }

    public void SetWonderRoomSource(object source, bool active)
    {
        if (!IsServer || source == null) return;
        if (active) wonderRoomSources.Add(source); else wonderRoomSources.Remove(source);
        flipAtkStat.Value = wonderRoomSources.Count > 0;
    }

    private float GetMeowsticPhysicalReduction()
    {
        int amount = 0;
        foreach (StatChange change in statChanges)
            if (change.AffectedStat == Stat.PhysicalDamageReduction && !change.Percentage)
                amount += change.IsBuff ? change.Amount : -change.Amount;
        return Mathf.Clamp01(amount / 100f);
    }
}
