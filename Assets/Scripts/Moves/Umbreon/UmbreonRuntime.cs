using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Authoritative passive/mark/basic state, including remote owners and bots.</summary>
public sealed class UmbreonRuntime : IDisposable
{
    private readonly PlayerManager actor;
    private readonly ushort passive = UmbreonEffectIds.Acquire(), haste = UmbreonEffectIds.Acquire(), team = UmbreonEffectIds.Acquire();
    private readonly Dictionary<Pokemon, ushort> marks = new Dictionary<Pokemon, ushort>();
    private double nextPassive, immuneUntil, nextHeal, nextTeam;
    private bool wasAlive = true, disposed, hasted;
    public int Charge { get; private set; }
    public UmbreonMeanLookEffect Zone { get; set; }
    private double Now => actor.NetworkManager.ServerTime.Time;
    public UmbreonRuntime(PlayerManager owner)
    {
        actor = owner;
        actor.Pokemon.BeforeStatusEffect += Status;
        actor.Pokemon.BeforeStatChange += StatFilter;
        actor.Pokemon.BeforeDisplacement += Displacement;
        actor.Pokemon.OnHealthDamageDealt += Damaged;
        actor.Pokemon.OnDeath += Death;
    }
    public void Tick()
    {
        if (disposed) return;
        bool living = UmbreonMoveRules.Living(actor.Pokemon);
        if (living && !wasAlive) { nextPassive = immuneUntil = 0; Charge = 0; Sync(); }
        wasAlive = living;
        if (!living) return;
        bool near = false;
        foreach (var entry in new List<KeyValuePair<Pokemon, ushort>>(marks))
        {
            if (!UmbreonMoveRules.Living(entry.Key) || !entry.Key.HasStatusEffect(entry.Value))
            { marks.Remove(entry.Key); UmbreonEffectIds.Release(entry.Value); continue; }
            near |= UmbreonMoveRules.InCircle(actor.transform.position, entry.Key.transform.position, UmbreonMoveRules.SwiftProximity);
        }
        if (near != hasted)
        {
            hasted = near;
            actor.Pokemon.SetPersistentStatRPC(new StatChange(15, Stat.Speed, 0, false, true, true, haste), near);
        }
        if (Now >= nextHeal)
        {
            nextHeal = Now + 1;
            if (actor.MovesController.HasUmbreonMove(AvailableMoves.UmbreonMoonlightPrance) && actor.Pokemon.IsOutOfCombat)
                actor.Pokemon.HealDamageRPC(Mathf.FloorToInt(actor.Pokemon.GetMaxHp() * 0.01f), actor.NetworkObjectId);
        }
        if (Now >= nextTeam) { nextTeam = Now + 1; TeamBonus(); }
    }
    private void TeamBonus()
    {
        int count = 0; bool eevees = true;
        if (GameManager.Instance != null)
            foreach (var player in GameManager.Instance.Players)
            {
                if (player == null || player == actor || !player.CurrentTeam.IsOnSameTeam(actor.CurrentTeam)) continue;
                count++;
                string name = player.Pokemon.BaseStats != null ? player.Pokemon.BaseStats.PokemonName.ToLowerInvariant() : "";
                eevees &= name == "espeon" || name == "sylveon" || name == "glaceon" || name == "leafeon" || name == "umbreon"
                    || name == "vaporeon" || name == "jolteon" || name == "flareon";
            }
        actor.Pokemon.SetPersistentStatRPC(new StatChange(1, Stat.MaxHp, 0, false, true, true, team, false), count == 4 && eevees);
    }
    public void Mark(Pokemon target)
    {
        if (!UmbreonMoveRules.Enemy(actor, target)) return;
        if (marks.TryGetValue(target, out var old)) { target.RemoveStatusEffectWithID(old); UmbreonEffectIds.Release(old); }
        ushort id = UmbreonEffectIds.Acquire();
        target.AddStatusEffect(new StatusEffect(StatusType.UmbreonSwiftMarked, UmbreonMoveRules.SwiftMarkDuration, true, id).WithSource(actor.NetworkObjectId));
        marks[target] = id;
        actor.MovesController.ShowUmbreonMarkRPC(target.NetworkObjectId, id);
    }
    private void Damaged(Pokemon target, DamageInfo damage, int hp)
    {
        if (marks.TryGetValue(target, out var id) && target.HasStatusEffect(id)) Ready();
    }
    public void Ready() { Charge = 2; Sync(); }
    public void SpendAttack() { Charge = (Charge + 1) % 3; Sync(); }
    private void Sync() { actor.MovesController.SynchronizeBasicAttackRPC(Charge, 0); }
    public bool InMeanZone => Zone != null && Zone.Active && UmbreonMoveRules.InCircle(Zone.Center, actor.transform.position, UmbreonMoveRules.MeanRadius);
    private bool Status(StatusEffect effect) => !UmbreonMoveRules.TriggersPassive(effect) || !Block();
    private StatChange? StatFilter(StatChange change) => !(change.HasSource && change.SourceId == actor.NetworkObjectId)
        && UmbreonMoveRules.TriggersPassive(change) && change.EffectiveAmount > 0 && Block() ? (StatChange?)null : change;
    private bool Displacement() => !Block();
    private bool Block()
    {
        if (disposed || !UmbreonMoveRules.Living(actor.Pokemon)) return false;
        if (Now < immuneUntil) return true;
        if (Now < nextPassive || actor.Pokemon.HasStatusEffect(StatusType.Unstoppable) || actor.Pokemon.HasStatusEffect(StatusType.Invincible)) return false;
        nextPassive = Now + UmbreonMoveRules.PassiveCooldown;
        bool evolved = actor.Pokemon.CurrentLevel >= 3;
        // Coalesce the paired stun + displacement of one attack, without granting Eevee an unstoppable status.
        immuneUntil = Now + (evolved ? UmbreonMoveRules.PassiveImmunity : 0.05f);
        if (evolved)
        {
            actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, UmbreonMoveRules.PassiveImmunity, true, passive));
            actor.Pokemon.AddStatChange(new StatChange(30, Stat.Defense, 3, true, true, true, passive));
            actor.Pokemon.AddStatChange(new StatChange(30, Stat.SpDefense, 3, true, true, true, passive));
        }
        actor.MovesController.ShowCombatVisualRPC(UmbreonMoveRules.Visuals + "Focus", actor.transform.position + Vector3.up,
            Vector3.forward, Vector3.one, evolved ? UmbreonMoveRules.PassiveImmunity : 0.3f);
        return true;
    }
    private void Death(DamageInfo damage)
    {
        wasAlive = false; Charge = 0; immuneUntil = 0; Sync(); ClearMarks();
        actor.Pokemon.RemoveStatusEffectWithID(passive);
        actor.Pokemon.RemoveAllStatChangeWithIDRPC(passive);
        hasted = false; actor.Pokemon.RemoveAllStatChangeWithIDRPC(haste);
    }
    private void ClearMarks()
    {
        foreach (var pair in marks)
        {
            if (pair.Key != null && pair.Key.IsSpawned) pair.Key.RemoveStatusEffectWithID(pair.Value);
            UmbreonEffectIds.Release(pair.Value);
        }
        marks.Clear();
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        actor.Pokemon.BeforeStatusEffect -= Status; actor.Pokemon.BeforeStatChange -= StatFilter;
        actor.Pokemon.BeforeDisplacement -= Displacement; actor.Pokemon.OnHealthDamageDealt -= Damaged; actor.Pokemon.OnDeath -= Death;
        ClearMarks();
        if (actor.Pokemon.IsSpawned)
        {
            actor.Pokemon.RemoveStatusEffectWithID(passive); actor.Pokemon.RemoveAllStatChangeWithIDRPC(passive);
            actor.Pokemon.RemoveAllStatChangeWithIDRPC(haste);
            actor.Pokemon.SetPersistentStatRPC(new StatChange(1, Stat.MaxHp, 0, false, true, true, team, false), false);
        }
        UmbreonEffectIds.Release(passive, 3); UmbreonEffectIds.Release(haste); UmbreonEffectIds.Release(team);
    }
}
