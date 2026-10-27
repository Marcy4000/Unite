using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class AbsolMidnightSlashEffect : PrefabMoveEffect
{
    [SerializeField] private GameObject flurry, finalFan;
    [SerializeField] private GameObject[] flurrySlashes;
    private Vector3 origin;

    protected override void BeginPresentation()
    {
        transform.SetPositionAndRotation(Actor.transform.position, Quaternion.LookRotation(Direction));
        if (flurry != null) { flurry.SetActive(false); flurry.transform.localScale = new Vector3(AbsolMoveRules.UniteWidth, 1, AbsolMoveRules.UniteLength); }
        if (finalFan != null) { finalFan.SetActive(false); finalFan.transform.localScale = Vector3.one * AbsolMoveRules.UniteFinalRange; }
    }

    protected override IEnumerator Execute()
    {
        origin = Actor.transform.position;
        // The gameplay root never dashes or retreats. The two imported clips
        // supply all apparent motion around this locked origin.
        LockActions(); FaceRPC(Direction); PlayAnimation(AbsolMoveRules.UniteFirstAnimation, blendDuration: AbsolMoveRules.UniteAnimationBlend);
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, AbsolMoveRules.UniteWindup, true, AbsolMoveRules.UniteStatusId));
        yield return new WaitForSeconds(AbsolMoveRules.UniteWindup);
        Actor.Pokemon.RemoveStatusEffectWithID(AbsolMoveRules.UniteStatusId);
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, AbsolMoveRules.UniteFlurryDuration, true, AbsolMoveRules.UniteStatusId));
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Untargetable, AbsolMoveRules.UniteFlurryDuration, true, AbsolMoveRules.UniteUntargetableId));
        for (int tick = 0; tick < AbsolMoveRules.UniteTicks; tick++)
        {
            FlurryRPC(tick);
            Physics.SyncTransforms();
            var targets = new List<Pokemon>();
            Vector3 center = origin + Direction * (AbsolMoveRules.UniteLength * 0.5f) + Vector3.up;
            foreach (var target in CombatTargeting.BoxTargets(center, new Vector3(AbsolMoveRules.UniteWidth * 0.5f, 2, AbsolMoveRules.UniteLength * 0.5f), Quaternion.LookRotation(Direction)))
                if (CanHit(target)) targets.Add(target);
            foreach (var target in targets)
            {
                var damage = AbsolMoveRules.UniteDamage(Actor.NetworkObjectId);
                damage.multiplier = 1f / targets.Count;
                target.ResolveDamage(damage);
            }
            yield return new WaitForSeconds(AbsolMoveRules.UniteFlurryDuration / AbsolMoveRules.UniteTicks);
        }
        Actor.Pokemon.RemoveStatusEffectWithID(AbsolMoveRules.UniteStatusId);
        Actor.Pokemon.RemoveStatusEffectWithID(AbsolMoveRules.UniteUntargetableId);
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, AbsolMoveRules.UniteSecondAnimationDuration, true, AbsolMoveRules.UniteStatusId));
        PlayAnimation(AbsolMoveRules.UniteSecondAnimation, blendDuration: AbsolMoveRules.UniteAnimationBlend);
        EndFlurryRPC();
        yield return new WaitForSeconds(AbsolMoveRules.UniteFinalDelay);
        FinalRPC();
        var finale = new Dictionary<Pokemon, int>();
        foreach (var target in CombatTargeting.SectorTargets(origin, Direction, AbsolMoveRules.UniteFinalRange, AbsolMoveRules.UniteFinalAngle))
            if (CanHit(target)) finale[target] = AbsolMoveRules.FinalHitCount(Vector3.Distance(origin, target.transform.position));
        for (int pulse = 0; pulse < 4; pulse++)
        {
            foreach (var entry in finale)
            {
                if (pulse >= entry.Value || !CanHit(entry.Key)) continue;
                var damage = AbsolMoveRules.UniteDamage(Actor.NetworkObjectId);
                damage.multiplier = pulse == 0 ? 1 : 0.3f;
                if (entry.Key.ResolveDamage(damage) > 0 && pulse == 0)
                    AbsolMoveRules.Shove(entry.Key, Direction, AbsolMoveRules.UniteShoveDistance, AbsolMoveRules.UniteShoveDuration);
            }
            if (pulse < 3) yield return new WaitForSeconds(AbsolMoveRules.UniteFinalSpacing);
        }
        yield return new WaitForSeconds(AbsolMoveRules.UniteRecovery);
        AbsolMoveRules.Refresh(Actor.Pokemon, new StatChange(80, Stat.Speed, 6, true, true, true, AbsolMoveRules.UniteSpeedId));
        Actor.Pokemon.RemoveShieldWithIDRPC(AbsolMoveRules.UniteShieldId);
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.2f), AbsolMoveRules.UniteShieldId, 0, 6, true));
        PlayAnimation(AbsolMoveRules.Idle, blendDuration: AbsolMoveRules.RecoveryAnimationBlend);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void FlurryRPC(int tick)
    {
        if (flurry != null) flurry.SetActive(true);
        for (int i = 0; i < flurrySlashes.Length; i++) if (flurrySlashes[i] != null) flurrySlashes[i].SetActive(i == tick % flurrySlashes.Length);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void EndFlurryRPC() { if (flurry != null) flurry.SetActive(false); }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void FinalRPC() { if (finalFan != null) finalFan.SetActive(true); }
    protected override void Cleanup()
    {
        if (!IsServer || Actor == null) return;
        Actor.Pokemon.RemoveStatusEffectWithID(AbsolMoveRules.UniteStatusId);
        Actor.Pokemon.RemoveStatusEffectWithID(AbsolMoveRules.UniteUntargetableId);
    }
}
