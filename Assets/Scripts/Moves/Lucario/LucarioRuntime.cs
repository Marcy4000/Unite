using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Per-caster server state. Marker availability is never shared between opposing Lucarios.</summary>
public sealed class LucarioRuntime : IDisposable
{
    private readonly PlayerManager actor;
    private readonly Dictionary<Pokemon, double> markers = new Dictionary<Pokemon, double>();
    private readonly ushort passiveId = LucarioEffectIds.Acquire();
    private readonly ushort extremeId = LucarioEffectIds.Acquire(), uniteId = LucarioEffectIds.Acquire();
    private double passiveReady, spinUntil, kickUntil;
    private bool disposed;
    public int Charge { get; private set; }
    public bool AuraEmpowered { get; private set; }
    public LucarioPowerUpPunchEffect Punch { get; set; }
    public double PassiveReadyAt => passiveReady;
    public bool SpinReady => Now < spinUntil;
    public bool KickReady => Now < kickUntil;
    private double Now => actor.NetworkManager.ServerTime.Time;
    public LucarioRuntime(PlayerManager owner)
    {
        actor = owner; actor.Pokemon.OnDamageReceived += Received; actor.Pokemon.OnDamageApplied += Dealt; actor.Pokemon.OnDeath += Death;
    }
    public void Tick()
    {
        if (disposed) return;
        foreach (var p in new List<Pokemon>(markers.Keys)) if (!LucarioMoveRules.Living(p)) markers.Remove(p);
        if (LucarioMoveRules.Living(actor.Pokemon))
        {
            TriggerPassive();
            if (actor.MovesController.HasLucarioMove(AvailableMoves.LucarioExtremeSpeed))
            {
                Physics.SyncTransforms();
                foreach (var p in CombatTargeting.SphereTargets(actor.transform.position + Vector3.up * 0.8f, LucarioMoveRules.MarkerRange))
                    if (LucarioMoveRules.Enemy(actor, p) && !markers.ContainsKey(p)) markers[p] = 0;
            }
        }
        Sync();
    }
    public bool MarkReady(Pokemon target) => markers.TryGetValue(target, out var ready) && Now >= ready;
    public bool ConsumeMarker(Pokemon target)
    {
        if (!MarkReady(target)) return false;
        markers[target] = Now + LucarioMoveRules.MarkerCooldown;
        actor.MovesController.ResetLucarioFirstMove(); return true;
    }
    public void RestoreMarker(Pokemon target)
    {
        if (LucarioMoveRules.Enemy(actor, target) && actor.MovesController.HasLucarioMove(AvailableMoves.LucarioExtremeSpeed)) markers[target] = 0;
    }
    public void ReadyBoosted() { Charge = 2; Sync(); }
    public void SpendBasic() { Charge = (Charge + 1) % 3; Sync(); }
    public void ReadySpin() { spinUntil = Now + LucarioMoveRules.ExtremeAttackWindow; Sync(); }
    public void ReadyKick() { kickUntil = Now + LucarioMoveRules.KickWindow; Sync(); }
    // Special attacks replace one attack without advancing or erasing the ordinary three-hit cycle.
    public void SpendSpecial(bool spin) { if (spin) spinUntil = 0; else kickUntil = 0; Sync(); }
    public void EmpowerAura() { AuraEmpowered = true; Sync(); }
    public void ExtremeBuff()
    {
        actor.Pokemon.RemoveAllStatChangeWithIDRPC(extremeId);
        actor.Pokemon.AddStatChange(new StatChange(LucarioMoveRules.ExtremeBuff, Stat.Attack, LucarioMoveRules.ExtremeBuffDuration, true, true, true, extremeId).WithSource(actor.NetworkObjectId));
    }
    public void UniteBuffs()
    {
        actor.Pokemon.RemoveAllStatChangeWithIDRPC(uniteId); actor.Pokemon.RemoveShieldWithIDRPC(uniteId);
        actor.Pokemon.AddStatChange(new StatChange(30, Stat.Speed, LucarioMoveRules.UniteBuffDuration, true, true, true, uniteId));
        actor.Pokemon.AddStatChange(new StatChange(30, Stat.Cdr, LucarioMoveRules.UniteBuffDuration, true, true, false, uniteId));
        actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(actor.Pokemon.GetMaxHp() * 0.2f), uniteId, 0, LucarioMoveRules.UniteBuffDuration, true).WithSource(actor.NetworkObjectId));
    }
    public bool SpendAura() { bool value = AuraEmpowered; AuraEmpowered = false; Sync(); return value; }
    public void Heal(int amount) { if (amount > 0 && LucarioMoveRules.Living(actor.Pokemon)) actor.Pokemon.HealDamageRPC(amount, actor.NetworkObjectId); }
    private void TriggerPassive()
    {
        if (Now < passiveReady || !LucarioMoveRules.Living(actor.Pokemon)
            || (float)actor.Pokemon.CurrentHp / Mathf.Max(1, actor.Pokemon.GetMaxHp()) > LucarioMoveRules.PassiveThreshold) return;
        passiveReady = Now + LucarioMoveRules.PassiveCooldown;
        actor.Pokemon.AddShieldRPC(new ShieldInfo(LucarioMoveRules.PassiveShield(actor.Pokemon.GetAttack(), actor.Pokemon.CurrentLevel), passiveId, 0, LucarioMoveRules.PassiveDuration, true).WithSource(actor.NetworkObjectId));
        actor.Pokemon.AddStatChange(new StatChange(20, Stat.Speed, LucarioMoveRules.PassiveDuration, true, true, true, passiveId).WithSource(actor.NetworkObjectId));
        actor.MovesController.ShowLucarioAttachedVisualRPC("Steadfast", "", LucarioMoveRules.PassiveDuration);
    }
    private void Received(DamageInfo damage, int amount) { if (amount > 0) TriggerPassive(); }
    private void Dealt(Pokemon target, DamageInfo damage, int amount)
    {
        if (amount <= 0 || !LucarioMoveRules.Living(actor.Pokemon) || target == actor.Pokemon || damage.source != DamageSource.Direct
            || damage.proprieties.HasFlag(DamageProprieties.IsBasicAttack) || damage.proprieties.HasFlag(DamageProprieties.IsHeal)) return;
        if (actor.MovesController.HasLucarioMove(AvailableMoves.LucarioPowerUpPunch)) actor.MovesController.ReduceLucarioFirstMove(1);
    }
    private void Sync() => actor.MovesController.SyncLucarioState(Charge, spinUntil, kickUntil, passiveReady, AuraEmpowered);
    public void RefreshMarkerPresentation()
    {
        foreach (var pair in markers)
            if (LucarioMoveRules.Enemy(actor, pair.Key)) actor.MovesController.ShowLucarioMarkerRPC(pair.Key.NetworkObjectId, pair.Value, LucarioMoveRules.MarkerRange);
    }
    private void Death(DamageInfo damage)
    {
        Charge = 0; spinUntil = kickUntil = 0; AuraEmpowered = false; markers.Clear();
        Punch = null; actor.Pokemon.RemoveShieldWithIDRPC(passiveId); actor.Pokemon.RemoveAllStatChangeWithIDRPC(passiveId); Sync();
        actor.Pokemon.RemoveAllStatChangeWithIDRPC(extremeId); actor.Pokemon.RemoveAllStatChangeWithIDRPC(uniteId); actor.Pokemon.RemoveShieldWithIDRPC(uniteId);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        actor.Pokemon.OnDamageReceived -= Received; actor.Pokemon.OnDamageApplied -= Dealt; actor.Pokemon.OnDeath -= Death;
        markers.Clear();
        foreach (ushort id in new[] { passiveId, extremeId, uniteId })
        {
            if (actor.Pokemon.IsSpawned) { actor.Pokemon.RemoveAllStatChangeWithIDRPC(id); actor.Pokemon.RemoveShieldWithIDRPC(id); }
            LucarioEffectIds.Release(id, LucarioMoveRules.UniteBuffDuration);
        }
    }
}

public static class LucarioEffectIds
{
    private static readonly Dictionary<ushort, float> used = new Dictionary<ushort, float>();
    public static ushort Acquire()
    {
        // Keep clear of Flygon/Trevenant/Cresselia/Jolteon's overlapping 36k–60k pools.
        // 5000–6799 is reserved here; legacy shield 6969 and Meowstic 7340+ remain untouched.
        for (ushort id = 5000; id < 6800; id++)
            if (!used.TryGetValue(id, out var until) || Time.time >= until) { used[id] = float.PositiveInfinity; return id; }
        throw new InvalidOperationException("Lucario effect IDs exhausted.");
    }
    public static void Release(ushort id, float delay = 0) { if (id != 0) used[id] = Time.time + delay; }
}
