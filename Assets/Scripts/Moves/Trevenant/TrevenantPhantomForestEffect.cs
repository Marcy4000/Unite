using System.Collections;
using UnityEngine;

public class TrevenantPhantomForestEffect : TrevenantMoveEffect
{
    protected override IEnumerator Execute()
    {
        LockActions(); SelfUnstoppable(TrevenantMoveRules.UniteWindup + TrevenantMoveRules.UniteRecovery);
        transform.position = Actor.transform.position + Direction;
        if (Direction.sqrMagnitude > 0.001f) FaceRPC(Direction.normalized);
        PlayAnimation(TrevenantMoveRules.Animation(Actor, "spellu1"), TrevenantMoveRules.UniteWindup * 0.5f);
        yield return CastPause(TrevenantMoveRules.UniteWindup * 0.5f);
        if (CastInterrupted) yield break;
        PlayAnimation(TrevenantMoveRules.Animation(Actor, "spellu2"), TrevenantMoveRules.UniteWindup * 0.5f);
        yield return CastPause(TrevenantMoveRules.UniteWindup * 0.5f);
        if (CastInterrupted) yield break;
        PhaseRPC(2); Physics.SyncTransforms(); int hits = 0;
        foreach (var target in CombatTargeting.SphereTargets(transform.position + Vector3.up * 0.8f, TrevenantMoveRules.UniteRadius))
        {
            if (!CanHit(target) || target.ResolveDamage(TrevenantMoveRules.Unite(Actor.NetworkObjectId)) <= 0) continue;
            hits++; Throw(target, TrevenantMoveRules.UniteThrow); Slow(target, 50, 2);
            if (TrevenantMoveRules.Living(target)) Combat.ForestMark(target);
        }
        Combat.ForestShield(hits);
        Haste(30, 6);
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.2f), EffectId, 0, 6, true).WithSource(Actor.NetworkObjectId));
        PlayAnimation(TrevenantMoveRules.Animation(Actor, "spellu3"), TrevenantMoveRules.UniteRecovery);
        yield return CastPause(TrevenantMoveRules.UniteRecovery); UnlockActions();
    }
}
