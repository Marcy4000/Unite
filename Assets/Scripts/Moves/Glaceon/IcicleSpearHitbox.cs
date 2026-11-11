using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

// Authoritative version of the personal cone rework: original damage, cadence,
// 80% cast slow and upgraded missing-HP bonus are intentionally preserved.
public class IcicleSpearHitbox : PrefabMoveEffect
{
    private CombatActionLock channelLock;
    private bool held;
    private readonly NetworkVariable<bool> firing = new NetworkVariable<bool>();
    private ushort slow;
    private GlaceonRuntime combat;
    public void ReleaseChannel() { if (IsServer && held) SetHeldRPC(false); }
    public override bool TryRecast(Vector3 direction)
    {
        if (combat == null || combat.Spear != this || combat.Count <= 0 || combat.SpearHeld) return false;
        combat.BeginSpear(direction);
        return true;
    }
    protected override IEnumerator Execute()
    {
        slow = GlaceonEffectIds.Acquire();
        combat = Actor.MovesController.GlaceCombat;
        if (combat == null) yield break;
        combat.Spear = this;
        bool empty = combat.Count == 0;
        if (empty) combat.AddCrystals(2);
        combat.RefreshCrystals();
        double now = NetworkManager.ServerTime.Time;
        double nextDrain = now + (empty ? 1 : 0), nextHit = now + 0.75, nextSlow = 0;
        Actor.MovesController.SyncGlaceSpearTick(nextDrain);
        OfferRecast();
        PlayAnimation("ani_spell1a1_bat_0471");
        var shape = GetComponent<Collider>();
        while (combat.Count > 0)
        {
            if (Actor.Pokemon.HasAnyStatusEffect(GlaceonMoveRules.Interrupts)) yield break;
            now = NetworkManager.ServerTime.Time;
            bool active = combat.SpearHeld;
            if (active != held) SetHeldRPC(active);
            transform.SetPositionAndRotation(Actor.transform.position, Quaternion.LookRotation(combat.SpearDirection));
            if (active)
            {
                if (now >= nextSlow)
                {
                    RefreshStat(Actor.Pokemon, new StatChange(80, Stat.Speed, 2, true, false, true, GlaceonMoveRules.SpearSlow));
                    nextSlow = now + 1;
                }
                if (now >= nextHit && shape != null)
                {
                    Physics.SyncTransforms();
                    var candidates = Physics.OverlapBox(shape.bounds.center, shape.bounds.extents, Quaternion.identity);
                    var hit = new System.Collections.Generic.HashSet<Pokemon>();
                    foreach (var collider in candidates)
                    {
                        var target = collider.GetComponentInParent<Pokemon>();
                        if (!CanHit(target) || hit.Contains(target) || !Physics.ComputePenetration(shape, shape.transform.position, shape.transform.rotation,
                            collider, collider.transform.position, collider.transform.rotation, out _, out _)) continue;
                        hit.Add(target);
                        if (target.ResolveDamage(GlaceonMoveRules.Damage(Actor.NetworkObjectId, AvailableMoves.GlaceIcycleSpear)) <= 0) continue;
                        if (!target.HasStatusEffect(StatusType.Unstoppable)) RefreshStat(target, new StatChange(20, Stat.Speed, 0.75f, true, false, true, slow));
                        if (Upgraded && GlaceonMoveRules.Living(target))
                        {
                            short damage = (short)Mathf.Clamp(Mathf.FloorToInt(target.GetMissingHp() * 0.05f), 0, target.Type == PokemonType.Wild ? 300 : short.MaxValue);
                            target.ResolveDamage(DamageInfo.Tuned("glaceon.iciclespearhitbox.releasechannel", new DamageInfo(Actor.NetworkObjectId, 0, 0, damage, DamageType.Special)));
                        }
                    }
                    nextHit = now + 0.75;
                }
            }
            if (now >= nextDrain)
            {
                combat.ConsumeCrystal(); nextDrain = now + 1;
                Actor.MovesController.SyncGlaceSpearTick(nextDrain);
            }
            combat.RefreshCrystals();
            yield return null;
        }
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void SetHeldRPC(bool active)
    {
        held = active;
        if (IsServer) firing.Value = active;
        channelLock?.Dispose(); channelLock = null;
        if (active) channelLock = new CombatActionLock(Actor, false, lockBattleItems: false, unlockedMoveSlot: 0);
        if (IsServer && !active && Actor != null) Actor.Pokemon.RemoveAllStatChangeWithIDRPC(GlaceonMoveRules.SpearSlow);
        foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = active;
    }
    protected override void Update()
    {
        base.Update();
        if (!IsClient) return;
        foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = firing.Value;
    }
    protected override void Cleanup()
    {
        channelLock?.Dispose(); channelLock = null;
        if (IsServer && Actor != null) Actor.MovesController.SyncGlaceSpearTick(0);
        if (IsServer && Actor != null && Actor.Pokemon != null && Actor.Pokemon.IsSpawned)
            Actor.Pokemon.RemoveAllStatChangeWithIDRPC(GlaceonMoveRules.SpearSlow);
        if (IsServer && combat != null && combat.Spear == this) combat.EndSpear();
        if (slow != 0) GlaceonEffectIds.ReleaseAfter(slow, 0.75f);
    }
}
