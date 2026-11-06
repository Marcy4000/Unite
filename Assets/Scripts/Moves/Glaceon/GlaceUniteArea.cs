using System.Collections;
using UnityEngine;

public class GlaceUniteArea : PrefabMoveEffect
{
    private ushort slow;
    private Collider volume;
    protected override IEnumerator Execute()
    {
        slow = GlaceonEffectIds.Acquire();
        volume = GetComponent<Collider>();
        transform.SetPositionAndRotation(Actor.transform.position, Quaternion.LookRotation(Direction));
        FaceRPC(Direction); PlayAnimation("ani_spellu_bat_0471", 0.3f);
        RefreshStat(Actor.Pokemon, new StatChange(40, Stat.Speed, 1, true, true, true, GlaceonMoveRules.UniteLeap));
        RefreshStat(Actor.Pokemon, new StatChange(30, Stat.Speed, 6, true, true, true, GlaceonMoveRules.UniteSpeed));
        RefreshStat(Actor.Pokemon, new StatChange(35, Stat.AtkSpeed, 6, true, true, true, GlaceonMoveRules.UniteAttackSpeed));
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.RoundToInt(Actor.Pokemon.GetMaxHp() * 0.2f), GlaceonMoveRules.UniteShield, 0, 6, true, 1));
        Physics.SyncTransforms();
        yield return new WaitForSeconds(0.2f);
        foreach (var target in InZone()) if (CanHit(target)) target.ResolveDamage(GlaceonMoveRules.Damage(Actor.NetworkObjectId, AvailableMoves.GlaceonUnite));
        float end = Time.time + 5.8f, nextCrystal = Time.time + 0.8f, nextSlow = 0;
        while (true)
        {
            bool inside = false;
            foreach (var target in InZone())
            {
                if (target == Actor.Pokemon) inside = true;
                else if (Time.time >= nextSlow && CanHit(target) && !target.HasStatusEffect(StatusType.Unstoppable))
                    RefreshStat(target, new StatChange(50, Stat.Speed, 2, true, false, true, slow));
            }
            if (inside)
            {
                if (!Actor.Pokemon.HasStatChangeWithID(GlaceonMoveRules.ZoneSpeed))
                    RefreshStat(Actor.Pokemon, new StatChange(50, Stat.Speed, 0, false, true, true, GlaceonMoveRules.ZoneSpeed));
                if (Time.time >= nextCrystal) Actor.MovesController.GlaceCombat?.AddCrystals(1);
            }
            else Actor.Pokemon.RemoveAllStatChangeWithIDRPC(GlaceonMoveRules.ZoneSpeed);
            if (Time.time >= nextCrystal) nextCrystal = Time.time + 1;
            if (Time.time >= nextSlow) nextSlow = Time.time + 1;
            if (Time.time >= end) break;
            yield return null;
        }
    }
    private System.Collections.Generic.HashSet<Pokemon> InZone()
    {
        var result = new System.Collections.Generic.HashSet<Pokemon>();
        if (volume == null) return result;
        foreach (var collider in Physics.OverlapBox(volume.bounds.center, volume.bounds.extents, Quaternion.identity))
        {
            var target = collider.GetComponentInParent<Pokemon>();
            if (target != null && Physics.ComputePenetration(volume, volume.transform.position, volume.transform.rotation,
                collider, collider.transform.position, collider.transform.rotation, out _, out _)) result.Add(target);
        }
        return result;
    }
    protected override void Cleanup()
    {
        if (IsServer && Actor != null && Actor.Pokemon != null && Actor.Pokemon.IsSpawned)
            Actor.Pokemon.RemoveAllStatChangeWithIDRPC(GlaceonMoveRules.ZoneSpeed);
        if (slow != 0) GlaceonEffectIds.ReleaseAfter(slow, 2);
    }
}
