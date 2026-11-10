using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrevenantHornLeechEffect : TrevenantMoveEffect
{
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction); transform.position = Actor.transform.position;
        SelfUnstoppable(TrevenantMoveRules.HornWindup + TrevenantMoveRules.HornTravel + TrevenantMoveRules.HornRecovery);
        PlayAnimation(TrevenantMoveRules.Animation(Actor, "spell2a1"), TrevenantMoveRules.HornWindup);
        yield return CastPause(TrevenantMoveRules.HornWindup);
        if (CastInterrupted || Actor.Pokemon.HasStatusEffect(StatusType.Immobilized)) yield break;
        PlayAnimation(TrevenantMoveRules.Animation(Actor, "spell2a2"), TrevenantMoveRules.HornTravel); PhaseRPC(1);
        Vector3 previous = Actor.transform.position;
        Drive(previous + Direction * TrevenantMoveRules.HornRange, TrevenantMoveRules.HornTravel, crossWalls: false);
        double until = NetworkManager.ServerTime.Time + TrevenantMoveRules.HornTravel, nextCarry = 0;
        var hits = new HashSet<Pokemon>(); var carried = new HashSet<Pokemon>();
        while (!CastInterrupted)
        {
            Physics.SyncTransforms();
            foreach (var target in CombatTargeting.SweptSphereTargets(previous + Vector3.up * 0.8f, Actor.transform.position + Vector3.up * 0.8f, TrevenantMoveRules.HornWidth * 0.5f))
            {
                if (!CanHit(target) || !hits.Add(target)) continue;
                if (target.ResolveDamage(TrevenantMoveRules.Horn(Actor.NetworkObjectId)) <= 0) continue;
                Combat.Recover(TrevenantMoveRules.HornHeal(TrevenantMoveRules.HpFraction(Actor.Pokemon)));
                Combat.EmpowerHorn();
                if (Stun(target, 0.15f)) carried.Add(target);
            }
            if (NetworkManager.ServerTime.Time >= nextCarry)
            {
                nextCarry = NetworkManager.ServerTime.Time + TrevenantMoveRules.HornCarryInterval;
                foreach (var target in carried)
                    if (Stun(target, TrevenantMoveRules.HornCarryInterval + 0.05f)) Displace(target, Actor.transform.position + Direction * TrevenantMoveRules.HornCarryOffset, TrevenantMoveRules.HornCarryInterval);
            }
            previous = Actor.transform.position;
            if (NetworkManager.ServerTime.Time >= until) break;
            yield return null;
        }
        if (CastInterrupted) yield break;
        PhaseRPC(2); PlayAnimation(TrevenantMoveRules.Animation(Actor, "spell2a3"), TrevenantMoveRules.HornRecovery);
        foreach (var target in carried)
        {
            if (!CanHit(target)) continue;
            target.ResolveDamage(TrevenantMoveRules.Horn(Actor.NetworkObjectId)); Throw(target, TrevenantMoveRules.HornThrow);
        }
        if (Upgraded) Haste(30, 1.5f);
        yield return CastPause(TrevenantMoveRules.HornRecovery); UnlockActions();
    }
    protected override void Update() { base.Update(); if (Actor != null) transform.position = Actor.transform.position; }
}
