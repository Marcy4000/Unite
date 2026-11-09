using System.Collections;
using UnityEngine;

public class UmbreonFakeTearsEffect : UmbreonMoveEffect
{
    protected override IEnumerator Execute()
    {
        ushort id = UmbreonEffectIds.Acquire();
        try
        {
            LockActions(); PlayAnimation(UmbreonMoveRules.Animation(Actor, "spell2"), 0.4f);
            Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.12f), id, 0, 4, true).WithSource(Actor.NetworkObjectId));
            yield return CastPause(0.2f);
            if (CastInterrupted) yield break;
            foreach (var target in CombatTargeting.SphereTargets(Actor.transform.position + Vector3.up * 0.8f, UmbreonMoveRules.TearsRadius))
            {
                if (!CanHit(target)) continue;
                target.ResolveDamage(UmbreonMoveRules.Tears(Actor.NetworkObjectId));
                target.AddStatChange(new StatChange(20, Stat.Defense, 2, true, false, true, id).WithSource(Actor.NetworkObjectId));
                target.AddStatChange(new StatChange(20, Stat.SpDefense, 2, true, false, true, id).WithSource(Actor.NetworkObjectId));
            }
            Visual("Tears", Actor.transform.position, UmbreonMoveRules.TearsRadius, 0.5f);
            yield return CastPause(0.2f);
        }
        finally { UnlockActions(); UmbreonEffectIds.Release(id, 4); }
    }
}
