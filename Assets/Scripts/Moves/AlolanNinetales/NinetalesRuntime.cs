using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One authoritative snow tracker per license, shared by all its damage sources.</summary>
public sealed class NinetalesRuntime : IDisposable
{
    private readonly PlayerManager actor;
    private readonly Dictionary<Pokemon, NinetalesSnowStacks> snow = new Dictionary<Pokemon, NinetalesSnowStacks>();
    private readonly HashSet<NinetalesVeilEffect> veils = new HashSet<NinetalesVeilEffect>();
    private readonly HashSet<Pokemon> affected = new HashSet<Pokemon>();
    private readonly ushort snowId = NinetalesEffectIds.Acquire(), slowId = NinetalesEffectIds.Acquire(), freezeId = NinetalesEffectIds.Acquire();
    private float nextPassive, veilUntil;
    private bool passivePending;
    private Vector3 passiveCenter;
    private float passiveImpactAt;
    private bool disposed;
    public ushort SnowId => snowId;
    public bool PassiveWarningActive => passivePending;
    public Vector3 PassiveCenter => passiveCenter;
    public bool VeilEmpowered => !disposed && Time.time < veilUntil;
    public NinetalesRuntime(PlayerManager player) { actor = player; actor.Pokemon.OnDeath += OnDeath; }

    public void Tick()
    {
        if (disposed) return;
        foreach (var target in new List<Pokemon>(snow.Keys))
            if (!NinetalesMoveRules.Living(target) || Time.time >= snow[target].Expires || !target.HasStatusEffect(snowId)) Clear(target);
        if (!NinetalesMoveRules.Living(actor.Pokemon)) { CancelPassiveWarning(); return; }
        if (passivePending)
        {
            if (Time.time < passiveImpactAt) return;
            ResolvePassive();
        }
        if (Time.time < nextPassive) return;
        // Passive acquisition deliberately ignores invisibility/grass, but not untargetability.
        Pokemon best = null; float score = float.MaxValue;
        foreach (var target in CombatTargeting.SphereTargets(actor.transform.position + Vector3.up * 0.8f, NinetalesMoveRules.PassiveRange))
        {
            if (!NinetalesMoveRules.CanHit(actor, target)) continue;
            float distance = (target.transform.position - actor.transform.position).sqrMagnitude;
            float priority = distance + (target.Type == PokemonType.Player ? 0 : 10000);
            if (priority < score) { best = target; score = priority; }
        }
        if (best == null) return;
        nextPassive = Time.time + NinetalesMoveRules.PassiveCooldown;
        passiveCenter = best.transform.position;
        passiveImpactAt = Time.time + NinetalesMoveRules.PassiveWarningDuration;
        passivePending = true;
        actor.MovesController.PresentNinetalesPassiveWarningRPC(true, passiveCenter, actor.NetworkManager.ServerTime.Time);
    }

    private void ResolvePassive()
    {
        // The selected enemy only seeds the position. Resolve against current
        // occupants of the fixed warning area, not a delayed sure-hit target.
        CancelPassiveWarning();
        Physics.SyncTransforms();
        foreach (var target in CombatTargeting.SphereTargets(passiveCenter + Vector3.up * 0.8f, NinetalesMoveRules.PassiveRadius))
            if (Hit(target, NinetalesMoveRules.PassiveDamage(actor.NetworkObjectId)) > 0) Slow(target, 30, 0.75f);
        actor.MovesController.ShowCombatVisualRPC(NinetalesMoveRules.Visuals + "Snowfall", passiveCenter, Vector3.forward,
            Vector3.one * NinetalesMoveRules.PassiveRadius, 0.45f);
    }

    private void CancelPassiveWarning()
    {
        if (!passivePending) return;
        passivePending = false;
        if (actor != null && actor.MovesController != null && actor.MovesController.IsSpawned)
            actor.MovesController.PresentNinetalesPassiveWarningRPC(false, passiveCenter, 0);
    }

    public int Hit(Pokemon target, DamageInfo damage, bool appliesSnow = true)
    {
        if (disposed || !NinetalesMoveRules.CanHit(actor, target)) return 0;
        // Damage must precede snow, especially the fourth-stack Snow Globe hit.
        int dealt = target.ResolveDamage(damage);
        if (dealt > 0 && appliesSnow && NinetalesMoveRules.Living(target)) AddSnow(target);
        return dealt;
    }

    public void AddSnow(Pokemon target)
    {
        if (disposed || !NinetalesMoveRules.CanHit(actor, target)) return;
        if (snow.TryGetValue(target, out var old) && (Time.time >= old.Expires || !target.HasStatusEffect(snowId))) Clear(target);
        if (!snow.TryGetValue(target, out var stacks)) { stacks = new NinetalesSnowStacks(); snow[target] = stacks; }
        bool freeze = stacks.Add(Time.time);
        target.RemoveStatusEffectWithID(snowId);
        if (freeze)
        {
            Clear(target);
            affected.Add(target);
            // No post-freeze immunity window. Shared status handling enforces unstoppable/resistance.
            target.RemoveStatusEffectWithID(freezeId);
            target.AddStatusEffect(new StatusEffect(StatusType.Frozen, NinetalesMoveRules.FreezeDuration, true, freezeId).WithSource(actor.NetworkObjectId));
            actor.MovesController.ShowCombatVisualRPC(NinetalesMoveRules.Visuals + "Freeze", target.transform.position, Vector3.forward, Vector3.one, NinetalesMoveRules.FreezeDuration);
        }
        else
        {
            target.AddStatusEffect(new StatusEffect(StatusType.SnowMarked, NinetalesMoveRules.SnowDuration, true, snowId).WithSource(actor.NetworkObjectId));
            if (target.HasStatusEffect(snowId)) Present(target, stacks.Count); else Clear(target);
        }
    }
    public int SnowCount(Pokemon target) => target != null && snow.TryGetValue(target, out var stacks) && Time.time < stacks.Expires
        && target.HasStatusEffect(snowId) ? stacks.Count : 0;
    public void Slow(Pokemon target, short amount, float duration)
    {
        if (NinetalesMoveRules.Living(target)) { affected.Add(target); NinetalesMoveRules.Refresh(target, new StatChange(amount, Stat.Speed, duration, true, false, true, slowId).WithSource(actor.NetworkObjectId)); }
    }
    private void Present(Pokemon target, int count)
    {
        if (target != null && actor != null && actor.IsSpawned && actor.MovesController.IsSpawned) actor.MovesController.PresentNinetalesSnowRPC(target.NetworkObjectId, count);
    }
    private void Clear(Pokemon target)
    {
        snow.Remove(target);
        if (target != null && target.IsSpawned) target.RemoveStatusEffectWithID(snowId);
        Present(target, 0);
    }
    public void EmpowerVeil()
    {
        veilUntil = Time.time + NinetalesMoveRules.VeilDuration;
        NinetalesMoveRules.Refresh(actor.Pokemon, new StatChange(15, Stat.AtkSpeed, NinetalesMoveRules.VeilDuration, true, true, true, NinetalesMoveRules.VeilHasteId));
    }
    public void SetInside(NinetalesVeilEffect veil, bool inside)
    {
        if (inside) veils.Add(veil); else veils.Remove(veil);
        if (actor == null || !actor.IsSpawned || !actor.Pokemon.IsSpawned) return;
        actor.MovesController.SetNinetalesVeilPresence(veils.Count > 0);
        if (veils.Count == 0) actor.Pokemon.RemoveAllStatChangeWithIDRPC(NinetalesMoveRules.VeilSpeedId);
        else if (!actor.Pokemon.HasStatChangeWithID(NinetalesMoveRules.VeilSpeedId))
            actor.Pokemon.AddStatChange(new StatChange(10, Stat.Speed, 0, false, true, true, NinetalesMoveRules.VeilSpeedId));
    }
    public void HealBoostedAttack()
    {
        if (VeilEmpowered && NinetalesMoveRules.Living(actor.Pokemon))
            actor.Pokemon.HealDamageRPC(NinetalesMoveRules.VeilHeal(actor.Pokemon.GetSpAttack(), actor.Pokemon.CurrentLevel), actor.NetworkObjectId);
    }
    private void OnDeath(DamageInfo damage) { Reset(); }
    private void Reset()
    {
        CancelPassiveWarning();
        foreach (var target in new List<Pokemon>(snow.Keys)) Clear(target);
        veilUntil = 0; nextPassive = Time.time + NinetalesMoveRules.PassiveCooldown; veils.Clear();
        if (actor != null && actor.IsSpawned && actor.Pokemon.IsSpawned)
        {
            actor.MovesController.SetNinetalesVeilPresence(false);
            actor.Pokemon.RemoveAllStatChangeWithIDRPC(NinetalesMoveRules.VeilSpeedId);
            actor.Pokemon.RemoveAllStatChangeWithIDRPC(NinetalesMoveRules.VeilHasteId);
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        Reset(); disposed = true;
        foreach (var target in affected)
            if (target != null && target.IsSpawned) { target.RemoveAllStatChangeWithIDRPC(slowId); target.RemoveStatusEffectWithID(freezeId); }
        affected.Clear();
        if (actor != null) actor.Pokemon.OnDeath -= OnDeath;
        NinetalesEffectIds.Release(snowId); NinetalesEffectIds.Release(slowId); NinetalesEffectIds.Release(freezeId);
    }
}
