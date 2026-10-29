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
        yield return TravelTo(Target, NinetalesMoveRules.BasicSpeed, () => CanHit(Target));
        if (!Arrived || !CanHit(Target)) yield break;
        var combat = Actor.MovesController.NinetalesCombat;
        if (combat == null) yield break;
        Vector3 impact = Target.transform.position;
        bool hit = false;
        var targets = Upgraded ? CombatTargeting.SphereTargets(Target.transform.position + Vector3.up * 0.8f, NinetalesMoveRules.BoostedRadius)
            : new System.Collections.Generic.HashSet<Pokemon>();
        targets.Add(Target);
        foreach (var target in targets)
        {
            if (combat.Hit(target, NinetalesMoveRules.BasicDamage(Actor.NetworkObjectId, Upgraded), Upgraded) <= 0) continue;
            hit = true;
            if (Upgraded) combat.Slow(target, 30, 0.75f);
        }
        if (hit && Upgraded) combat.HealBoostedAttack(); // Once, never per splash victim.
        Actor.MovesController.ShowCombatVisualRPC(NinetalesMoveRules.Visuals + "IceImpact", impact, Vector3.forward,
            Vector3.one * (Upgraded ? NinetalesMoveRules.BoostedRadius : 0.4f), 0.25f);
    }
}
