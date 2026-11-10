using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrevenantBranchPokeEffect : TrevenantMoveEffect
{
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction); transform.position = Actor.transform.position;
        PlayAnimation(TrevenantMoveRules.Animation(Actor, "spell21"), TrevenantMoveRules.BranchWindup);
        yield return CastPause(TrevenantMoveRules.BranchWindup);
        if (CastInterrupted || Actor.Pokemon.HasStatusEffect(StatusType.Immobilized)) yield break;
        PlayAnimation(TrevenantMoveRules.Animation(Actor, "spell22"), TrevenantMoveRules.BranchTravel); PhaseRPC(1);
        Vector3 destination = Actor.PlayerMovement.GetWallCrossingDestination(Actor.transform.position + Direction * TrevenantMoveRules.BranchRange);
        Vector3 previous = Actor.transform.position;
        Drive(destination, TrevenantMoveRules.BranchTravel);
        double until = NetworkManager.ServerTime.Time + TrevenantMoveRules.BranchTravel;
        var hits = new HashSet<Pokemon>();
        while (!CastInterrupted)
        {
            Physics.SyncTransforms();
            foreach (var target in CombatTargeting.SweptSphereTargets(previous + Vector3.up * 0.8f, Actor.transform.position + Vector3.up * 0.8f, TrevenantMoveRules.BranchRadius))
            {
                if (!CanHit(target) || !hits.Add(target)) continue;
                if (target.ResolveDamage(TrevenantMoveRules.Branch(Actor.NetworkObjectId)) > 0)
                { Slow(target, 40, 2); Combat.Recover(0.03f); }
            }
            previous = Actor.transform.position;
            if (NetworkManager.ServerTime.Time >= until) break;
            yield return null;
        }
        if (CastInterrupted) yield break;
        PhaseRPC(2); PlayAnimation(TrevenantMoveRules.Animation(Actor, "spell23"), TrevenantMoveRules.BranchRecovery);
        yield return CastPause(TrevenantMoveRules.BranchRecovery); UnlockActions();
    }
    protected override void Update() { base.Update(); if (Actor != null) transform.position = Actor.transform.position; }
}
