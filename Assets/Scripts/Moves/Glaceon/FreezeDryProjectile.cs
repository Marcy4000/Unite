using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Server-owned swept projectile, single explosion, then a three-second ice zone.
public class FreezeDryProjectile : PrefabMoveEffect
{
    public float speed = 30;
    private readonly Dictionary<Pokemon, float> entered = new Dictionary<Pokemon, float>();
    private readonly HashSet<Pokemon> frozen = new HashSet<Pokemon>();
    private ushort slow, freeze;
    private readonly Unity.Netcode.NetworkVariable<bool> zoneActive = new Unity.Netcode.NetworkVariable<bool>();
    private bool exploded;
    private const float Radius = 1;
    protected override IEnumerator Execute()
    {
        slow = GlaceonEffectIds.Acquire(); freeze = GlaceonEffectIds.Acquire();
        LockActions(); FaceRPC(Direction); PlayAnimation("ani_spell1b_bat_0471", 0.35f);
        Vector3 start = Actor.transform.position + Vector3.up * 0.8f;
        transform.position = start;
        float travelled = 0, recovery = 0;
        while (!exploded && travelled < 6)
        {
            float step = Mathf.Min(speed * Time.deltaTime, 6 - travelled);
            Vector3 next = transform.position + Direction * step;
            foreach (var target in CombatTargeting.SweptSphereTargets(transform.position, next, 0.3f))
                if (CanHit(target)) { exploded = true; break; }
            transform.position = next; travelled += step;
            recovery += Time.deltaTime;
            if (recovery >= 0.35f) UnlockActions();
            if (!exploded && travelled < 6) yield return null;
        }
        // No collision callback can resolve the same explosion twice.
        exploded = true;
        int hits = 0;
        foreach (var target in GlaceonMoveRules.Targets(Physics.OverlapSphere(transform.position, Radius)))
            if (CanHit(target) && target.ResolveDamage(GlaceonMoveRules.Damage(Actor.NetworkObjectId, AvailableMoves.GlaceFreezeDry)) > 0) hits++;
        if (hits > 0)
        {
            RefreshStat(Actor.Pokemon, new StatChange(50, Stat.SpAttack, 4, true, true, true, GlaceonMoveRules.FreezeAttack));
            Actor.MovesController.GlaceCombat?.AddCrystals(hits * (Upgraded ? 3 : 2));
        }
        zoneActive.Value = true;
        float until = Time.time + 3;
        while (Time.time < until)
        {
            if (recovery < 0.35f) { recovery += Time.deltaTime; if (recovery >= 0.35f) UnlockActions(); }
            var inside = GlaceonMoveRules.Targets(Physics.OverlapSphere(transform.position, Radius));
            foreach (var target in new List<Pokemon>(entered.Keys)) if (!inside.Contains(target) || !CanHit(target)) entered.Remove(target);
            foreach (var target in inside)
            {
                if (!CanHit(target)) continue;
                if (!entered.ContainsKey(target))
                {
                    entered[target] = Time.time;
                    if (!target.HasStatusEffect(StatusType.Unstoppable)) RefreshStat(target, new StatChange(30, Stat.Speed, 3, true, false, true, slow));
                }
                if (Time.time - entered[target] >= 2.5f && frozen.Add(target))
                    target.AddStatusEffect(new StatusEffect(StatusType.Frozen, 0.6f, true, freeze).WithSource(Actor.NetworkObjectId));
            }
            yield return null;
        }
    }
    protected override void Update()
    {
        base.Update();
        var projectile = transform.Find("ProjectileGraphics");
        var zone = transform.Find("IceZone");
        if (projectile != null) projectile.gameObject.SetActive(!zoneActive.Value);
        if (zone != null) zone.gameObject.SetActive(zoneActive.Value);
    }
    protected override void Cleanup()
    {
        entered.Clear(); frozen.Clear();
        if (slow != 0) GlaceonEffectIds.ReleaseAfter(slow, 3);
        if (freeze != 0) GlaceonEffectIds.ReleaseAfter(freeze, 0.6f);
    }
}
