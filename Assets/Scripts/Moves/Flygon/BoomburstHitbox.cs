using System.Collections;
using UnityEngine;

public class BoomburstHitbox : FlygonMoveObject
{
    [SerializeField] private ParticleSystem particles;
    public override FlygonCast Kind => FlygonCast.Boomburst;
    protected override void PresentPhase(byte value) { if (value == 1 && particles != null) particles.Play(true); }
    protected override IEnumerator Execute()
    {
        yield return Wait(FlygonMoveRules.Windup(Kind), true);
        if (!Live) yield break;
        transform.position = Actor.transform.position;
        SetPhase(1);
        foreach (Pokemon target in TargetsInArea())
        {
            float distance = Vector3.Distance(transform.position, target.transform.position);
            target.TakeDamageRPC(FlygonMoveRules.Damage(Actor.NetworkObjectId, Kind, distance <= 3 ? 0 : 1));
            if (!CanHit(target)) continue;
            short reduction = FlygonMoveRules.BurstReduction(distance);
            if (reduction > 0)
            {
                RefreshStat(target, Stat.Defense, reduction, FlygonMoveRules.BurstReductionDuration(Upgraded));
                RefreshStat(target, Stat.SpDefense, reduction, FlygonMoveRules.BurstReductionDuration(Upgraded));
            }
            if (!CanControl(target)) continue;
            target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.2f, true, EffectId));
            if (target.TryGetComponent(out PlayerMovement movement)) movement.FlygonDisplaceRPC(transform.position, Mathf.Lerp(1.3f, 0.25f, Mathf.Clamp01(distance / 5)), false);
        }
        yield return Wait(0.6f);
    }
}
