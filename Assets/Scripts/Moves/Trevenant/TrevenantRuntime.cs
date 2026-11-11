using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Server-owned passive, delayed healing, attack bonuses and source-specific forest marks.</summary>
public sealed class TrevenantRuntime : IDisposable
{
    private readonly PlayerManager actor;
    private readonly List<Recovery> recoveries = new List<Recovery>();
    private readonly Dictionary<Pokemon, Mark> marks = new Dictionary<Pokemon, Mark>();
    private readonly Dictionary<ulong, double> markTriggers = new Dictionary<ulong, double>();
    private readonly ushort shield = TrevenantEffectIds.Acquire();
    private double passiveReady, nextPassiveHit, nextHeal, nextReduction;
    private int passiveTicks, reductions;
    private bool recovering, disposed;
    public int Charge { get; private set; }
    public int HornAttacks { get; private set; }
    public TrevenantCurseEffect Curse { get; set; }
    public TrevenantPainSplitEffect Link { get; set; }
    private double Now => actor.NetworkManager.ServerTime.Time;
    public double PassiveReadyAt => passiveReady;
    public TrevenantRuntime(PlayerManager owner)
    {
        actor = owner;
        actor.Pokemon.OnDamageReceived += Received;
        actor.Pokemon.OnDamageApplied += Dealt;
        actor.Pokemon.ModifyIncomingDamage += Redirect;
        actor.Pokemon.ModifyHealingReceived += AmplifyHealing;
        actor.Pokemon.OnDeath += Death;
    }
    public void Tick()
    {
        if (disposed) return;
        foreach (var pair in new List<KeyValuePair<Pokemon, Mark>>(marks))
            if (!TrevenantMoveRules.Living(pair.Key) || Now >= pair.Value.Until || !pair.Key.HasStatusEffect(pair.Value.Id)) RemoveMark(pair.Key);
        if (!TrevenantMoveRules.Living(actor.Pokemon)) { Sync(); return; }
        if (recovering)
        {
            while (passiveTicks < 8 && Now >= nextHeal)
            { passiveTicks++; nextHeal += TrevenantMoveRules.PassiveHealInterval; HealFraction(TrevenantMoveRules.PassiveHeal(actor.Pokemon.CurrentLevel)); }
            while (reductions < 4 && Now >= nextReduction)
            { reductions++; nextReduction += 1; actor.MovesController.ReduceTrevenantCooldown(0, 1); actor.MovesController.ReduceTrevenantCooldown(1, 1); }
            if (passiveTicks == 8 && reductions == 4) { recovering = false; passiveReady = Now + TrevenantMoveRules.PassiveCooldown; }
        }
        for (int i = recoveries.Count - 1; i >= 0; i--)
        {
            var heal = recoveries[i];
            while (heal.Left > 0 && Now >= heal.Next)
            { heal.Left--; heal.Next += 1; HealFraction(heal.Fraction); }
            if (heal.Left == 0) recoveries.RemoveAt(i);
        }
        Sync();
    }
    private void Sync() => actor.MovesController.SyncTrevenantState(Curse != null ? Curse.Gauge : -1, recovering ? -1 : passiveReady);
    public void PayCost(float fraction, bool maximum)
    {
        int amount = TrevenantMoveRules.HealthCost(actor.Pokemon.CurrentHp, actor.Pokemon.GetMaxHp(), fraction, maximum);
        if (actor.Pokemon.PayHealthCost(amount) > 0) Curse?.RecordDamageEvent();
    }
    public void HealFraction(float fraction) => actor.Pokemon.HealDamageRPC(Mathf.FloorToInt(actor.Pokemon.GetMaxHp() * fraction), actor.NetworkObjectId);
    public void Recover(float fractionPerTick) => recoveries.Add(new Recovery { Fraction = fractionPerTick, Next = Now + 1, Left = 3 });
    public void EmpowerHorn() { HornAttacks = 3; SyncCharge(); }
    public void SpendAttack() { Charge = (Charge + 1) % 3; SyncCharge(); }
    public bool SpendHornAttack() { if (HornAttacks == 0) return false; HornAttacks--; SyncCharge(); return true; }
    private void SyncCharge() => actor.MovesController.SynchronizeBasicAttackRPC(Charge, 0);
    private void TriggerPassive()
    {
        if (recovering || Now < passiveReady || !TrevenantMoveRules.Living(actor.Pokemon)
            || TrevenantMoveRules.HpFraction(actor.Pokemon) > TrevenantMoveRules.PassiveThreshold) return;
        recovering = true; passiveTicks = reductions = 0;
        nextHeal = Now + TrevenantMoveRules.PassiveHealInterval; nextReduction = Now + 1;
        actor.MovesController.ShowCombatVisualRPC(TrevenantMoveRules.Visuals + "Healing", actor.transform.position, Vector3.forward, Vector3.one, TrevenantMoveRules.PassiveDuration);
    }
    private void Received(DamageInfo damage, int amount)
    {
        if (amount <= 0) return;
        Curse?.RecordDamageEvent(); TriggerPassive();
    }
    private void Dealt(Pokemon target, DamageInfo damage, int amount)
    {
        if (amount <= 0 || target == actor.Pokemon || !TrevenantMoveRules.Living(actor.Pokemon)) return;
        Curse?.RecordDamageEvent(); TriggerPassive();
        if (!recovering && Now < passiveReady && Now >= nextPassiveHit && damage.source == DamageSource.Direct
            && !damage.proprieties.HasFlag(DamageProprieties.IsBasicAttack))
        { passiveReady = Math.Max(Now, passiveReady - 1); nextPassiveHit = Now + TrevenantMoveRules.PassiveReductionInterval; }
    }
    private int Redirect(DamageInfo damage, int amount)
    {
        return Link != null ? Link.RedirectDamage(amount) : amount;
    }
    private int AmplifyHealing(int amount) => actor.Pokemon.HasShieldWithID(shield) ? Mathf.FloorToInt(amount * 1.5f) : amount;
    public void ForestShield(int enemies)
    {
        if (enemies <= 0) return;
        actor.Pokemon.RemoveShieldWithIDRPC(shield);
        int amount = Mathf.FloorToInt((0.8f * actor.Pokemon.GetAttack() + 500) * Mathf.Min(5, enemies));
        actor.Pokemon.AddShieldRPC(new ShieldInfo(amount, shield, 0, TrevenantMoveRules.UniteShieldDuration, true).WithSource(actor.NetworkObjectId));
    }
    public void ForestMark(Pokemon target)
    {
        RemoveMark(target);
        var mark = new Mark { Id = TrevenantEffectIds.Acquire(), Until = Now + TrevenantMoveRules.UniteMarkDuration };
        mark.Handler = (damage, amount) => MarkHit(target, damage, amount);
        marks[target] = mark;
        target.AddStatusEffect(new StatusEffect(StatusType.TrevenantForestMarked, TrevenantMoveRules.UniteMarkDuration, true, mark.Id).WithSource(actor.NetworkObjectId));
        target.OnDamageReceived += mark.Handler;
        actor.MovesController.ShowTrevenantMarkRPC(target.NetworkObjectId, mark.Id);
    }
    private void MarkHit(Pokemon target, DamageInfo damage, int amount)
    {
        if (amount <= 0 || !marks.TryGetValue(target, out var mark) || !target.HasStatusEffect(mark.Id)
            || damage.source != DamageSource.Direct || damage.suppressOnHitEffects || !damage.proprieties.HasFlag(DamageProprieties.IsBasicAttack)
            || !TrevenantMoveRules.Living(target) || (markTriggers.TryGetValue(damage.attackerId, out var next) && Now < next)) return;
        if (!actor.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(damage.attackerId, out var obj)
            || !obj.TryGetComponent(out Pokemon attacker) || !attacker.TeamMember.IsOnSameTeam(actor.CurrentTeam)) return;
        markTriggers[damage.attackerId] = Now + TrevenantMoveRules.UniteMarkInterval;
        var bonus = DamageInfo.Tuned("trevenant.runtime.naturalcure", TrevenantMoveRules.Damage(damage.attackerId, 0, 0, (short)(190 + 8 * actor.Pokemon.CurrentLevel)));
        bonus.source = DamageSource.Secondary; bonus.suppressOnHitEffects = true;
        target.ResolveDamage(bonus);
    }
    private void RemoveMark(Pokemon target)
    {
        if (!marks.TryGetValue(target, out var mark)) return;
        if (target != null) { target.OnDamageReceived -= mark.Handler; if (target.IsSpawned) target.RemoveStatusEffectWithID(mark.Id); }
        marks.Remove(target); TrevenantEffectIds.Release(mark.Id);
    }
    private void Death(DamageInfo damage)
    {
        if (recovering) { recovering = false; passiveReady = Now + TrevenantMoveRules.PassiveCooldown; }
        recoveries.Clear(); Charge = HornAttacks = 0; SyncCharge();
        foreach (var target in new List<Pokemon>(marks.Keys)) RemoveMark(target);
        markTriggers.Clear();
        if (actor.Pokemon.IsSpawned) actor.Pokemon.RemoveShieldWithIDRPC(shield);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        actor.Pokemon.OnDamageReceived -= Received; actor.Pokemon.OnDamageApplied -= Dealt;
        actor.Pokemon.ModifyIncomingDamage -= Redirect; actor.Pokemon.ModifyHealingReceived -= AmplifyHealing;
        actor.Pokemon.OnDeath -= Death;
        foreach (var target in new List<Pokemon>(marks.Keys)) RemoveMark(target);
        recoveries.Clear(); TrevenantEffectIds.Release(shield, TrevenantMoveRules.UniteShieldDuration);
    }
    private sealed class Recovery { public double Next; public float Fraction; public int Left; }
    private sealed class Mark { public ushort Id; public double Until; public Action<DamageInfo, int> Handler; }
}

public static class TrevenantEffectIds
{
    private static readonly Dictionary<ushort, float> used = new Dictionary<ushort, float>();
    public static ushort Acquire()
    {
        for (ushort id = 45000; id < 47000; id++)
            if (!used.TryGetValue(id, out var until) || Time.time >= until) { used[id] = float.PositiveInfinity; return id; }
        throw new InvalidOperationException("Trevenant effect IDs exhausted.");
    }
    public static void Release(ushort id, float delay = 0) { if (id != 0) used[id] = Time.time + delay; }
}
