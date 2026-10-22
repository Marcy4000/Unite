using System.Collections;
using UnityEngine;

/// <summary>Configurable sure-hit projectile, using existing damage and stat records.</summary>
public class HomingMoveEffect : PrefabMoveEffect
{
    [SerializeField] private DamageInfo damage;
    [SerializeField] private StatChange[] onHitStats;
    [SerializeField] private float speed = 20;

    protected override IEnumerator Execute()
    {
        yield return TravelTo(Target, speed, () => CanHit(Target));
        if (!Arrived || !CanHit(Target)) yield break;
        damage.attackerId = Actor.Pokemon.NetworkObjectId;
        Target.TakeDamageRPC(damage);
        if (Target.CurrentHp > 0 && onHitStats != null)
            foreach (StatChange stat in onHitStats) RefreshStat(Target, stat);
    }
}
