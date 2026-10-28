using System.Collections.Generic;
using UnityEngine;

public partial class Pokemon
{
    private sealed class PeriodicStatus
    {
        public StatusEffect Effect;
        public PeriodicDamageClock Clock;
    }
    private readonly List<PeriodicStatus> periodicStatuses = new List<PeriodicStatus>();
    private PeriodicStatus[] periodicSnapshot = new PeriodicStatus[4];

    private void RegisterPeriodicStatus(StatusEffect effect, bool refresh = false)
    {
        double now = Time.timeAsDouble;
        foreach (var state in periodicStatuses.ToArray())
            if (state.Effect.SamePeriodicSource(effect))
            {
                if (!refresh) { periodicStatuses.Remove(state); break; }
                state.Effect = effect; state.Clock.Refresh(now, effect.Duration, effect.PeriodicDamage.Interval); return;
            }
        periodicStatuses.Add(new PeriodicStatus { Effect = effect, Clock = new PeriodicDamageClock(now, effect.Duration, effect.PeriodicDamage.Interval) });
    }

    private float PeriodicTimeRemaining(StatusEffect effect)
    {
        foreach (var state in periodicStatuses)
            if (state.Effect.SamePeriodicSource(effect)) return (float)(state.Clock.Expires - Time.timeAsDouble);
        return 0;
    }

    private void SetPeriodicTime(StatusEffect effect, float remaining)
    {
        foreach (var state in periodicStatuses)
            if (state.Effect.SamePeriodicSource(effect)) state.Clock.SetRemaining(Time.timeAsDouble, remaining);
    }

    public bool HasDamageOverTime(StatusType type, ulong sourceId)
    {
        for (int i = 0; i < statusEffects.Count; i++)
        {
            StatusEffect effect = statusEffects[i];
            if (effect.Type == type && effect.PeriodicDamage.Enabled && effect.PeriodicDamage.Damage.attackerId == sourceId) return true;
        }
        return false;
    }

    // Server API for hit-confirmed maintenance, never adds a new burn or shortens another source's burn.
    public void RefreshDamageOverTime(StatusType type, ulong sourceId, float minimumRemaining)
    {
        if (!IsServer || !float.IsFinite(minimumRemaining) || minimumRemaining <= 0) return;
        foreach (var state in periodicStatuses)
        {
            if (state.Effect.Type != type || state.Effect.PeriodicDamage.Damage.attackerId != sourceId || !HasStatusEffect(state.Effect)) continue;
            state.Clock.Extend(Time.timeAsDouble, minimumRemaining);
            int index = statusEffects.IndexOf(state.Effect);
            statusTimers[index] = Mathf.Max(statusTimers[index], minimumRemaining);
        }
    }

    public void RemoveDamageOverTime(StatusType type, ulong sourceId)
    {
        if (!IsServer) return;
        for (int i = statusEffects.Count - 1; i >= 0; i--)
        {
            StatusEffect effect = statusEffects[i];
            if (effect.Type == type && effect.PeriodicDamage.Enabled && effect.PeriodicDamage.Damage.attackerId == sourceId)
                RemoveStatusEffectRPC(effect);
        }
    }

    private void TickPeriodicStatuses()
    {
        if (periodicStatuses.Count == 0) return;
        // Damage can cause death, cleanse and new statuses. Never enumerate the live collection through those callbacks.
        int count = periodicStatuses.Count;
        if (periodicSnapshot.Length < count) System.Array.Resize(ref periodicSnapshot, Mathf.NextPowerOfTwo(count));
        periodicStatuses.CopyTo(periodicSnapshot);
        for (int i = 0; i < count; i++)
        {
            var state = periodicSnapshot[i];
            StatusEffect effect = state.Effect;
            if (CurrentHp <= 0 || baseStats == null || !HasStatusEffect(effect)
                || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(effect.PeriodicDamage.Damage.attackerId, out var obj)
                || !obj.TryGetComponent(out Pokemon source) || source.BaseStats == null)
            {
                if (HasStatusEffect(effect)) RemoveStatusEffectRPC(effect);
                periodicStatuses.Remove(state); continue;
            }
            int catchup = 0;
            while (CurrentHp > 0 && HasStatusEffect(effect) && catchup++ < 32 && state.Clock.TryTick(Time.timeAsDouble))
                TakeDamageRPC(effect.PeriodicDamage.TickDamage(GetMaxHp(), (effect.PeriodicDamage.Damage.type == DamageType.Special) != source.SwitchAtkStat ? source.GetSpAttack() : source.GetAttack(), source.CurrentLevel, Type != PokemonType.Player));
        }
        System.Array.Clear(periodicSnapshot, 0, count);
    }
}
