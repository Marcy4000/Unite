using System.Collections;
using UnityEngine;

public class SylvDrainingKissEffect : PrefabMoveEffect
{
    private bool Linked => CanHit(Target) && Actor != null
        && Vector3.Distance(Actor.transform.position, Target.transform.position) <= SylvMoveRules.KissBreakRange;

    protected override IEnumerator Execute()
    {
        for (int hit = 0; hit < 3 && Linked; hit++)
        {
            yield return TravelTo(Target, SylvMoveRules.KissSpeed, () => Linked);
            if (!Arrived || !Linked) yield break;
            Target.TakeDamageRPC(SylvMoveRules.KissDamage(Actor.Pokemon.NetworkObjectId));
            if (Target.CurrentHp > 0)
                Target.AddDecayingStatRPC(new StatChange(50, Stat.Speed, 1, true, false, true, SylvMoveRules.KissSlow), 35, 0.5f);
            // A lethal hit may return its earned heal, but cannot start another bounce.
            yield return TravelTo(Actor.Pokemon, SylvMoveRules.KissSpeed, () => Target != null && Target.IsSpawned
                && Vector3.Distance(Actor.transform.position, Target.transform.position) <= SylvMoveRules.KissBreakRange);
            if (!Arrived) yield break;
            Actor.Pokemon.HealDamageRPC(SylvMoveRules.KissHeal(Actor.Pokemon.NetworkObjectId, Upgraded));
            yield return new WaitForSeconds(0.1f);
        }
    }
}
