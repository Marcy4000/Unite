using System.Collections;
using UnityEngine;

public class NinetalesBasicProjectile : PrefabMoveEffect
{
    [SerializeField] private GameObject normal, boosted;
    protected override void BeginPresentation()
    {
        if (normal != null) normal.SetActive(!Upgraded);
        if (boosted != null) boosted.SetActive(Upgraded);
    }
    protected override IEnumerator Execute()
    {
        Vector3 forward = Target != null ? Target.transform.position - Actor.transform.position : Direction;
        forward.y = 0;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Actor.transform.forward;
        yield return TravelTo(Target, NinetalesMoveRules.BasicSpeed, () => CanHit(Target));
        if (!Arrived || !CanHit(Target)) yield break;
        var combat = Actor.MovesController.NinetalesCombat;
        if (combat == null) yield break;
        Vector3 impact = Target.transform.position;
        bool hit = false;
        Physics.SyncTransforms();
        var targets = Upgraded ? CombatTargeting.BoxTargets(impact + forward * NinetalesMoveRules.BoostedForwardOffset + Vector3.up * 0.8f,
            new Vector3(NinetalesMoveRules.BoostedWidth * 0.5f, 1.2f, NinetalesMoveRules.BoostedLength * 0.5f), Quaternion.LookRotation(forward))
            : new System.Collections.Generic.HashSet<Pokemon>();
        targets.Add(Target);
        foreach (var target in targets)
        {
            if (combat.Hit(target, NinetalesMoveRules.BasicDamage(Actor.NetworkObjectId, Upgraded), Upgraded) <= 0) continue;
            hit = true;
            if (Upgraded) combat.Slow(target, 30, 0.75f);
        }
        if (hit && Upgraded) combat.HealBoostedAttack(); // Once, never per splash victim.
        Actor.MovesController.ShowCombatVisualRPC(NinetalesMoveRules.Visuals + "IceImpact",
            impact + (Upgraded ? forward * NinetalesMoveRules.BoostedForwardOffset : Vector3.zero), forward,
            Upgraded ? new Vector3(NinetalesMoveRules.BoostedWidth * 0.5f, 1, NinetalesMoveRules.BoostedLength * 0.5f) : Vector3.one * 0.4f, 0.25f);
    }
}
