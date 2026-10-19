using System.Collections;
using UnityEngine;

public class DuraDragonTailObject : DuraMoveObject
{
    [SerializeField] private BoxCollider tailArea;
    [SerializeField] private Transform tailSweep;
    [SerializeField] private float effectDuration = 0.3f;

    protected override void BeginPresentation()
    {
        StartCoroutine(AnimateSweep());
    }

    private IEnumerator AnimateSweep()
    {
        float started = Time.time;
        while (Time.time - started < effectDuration)
        {
            float progress = Mathf.Clamp01((Time.time - started) / effectDuration);
            tailSweep.localRotation = Quaternion.Euler(0, Mathf.Lerp(-35f, 35f, progress), 0);
            tailSweep.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, Mathf.Sin(progress * Mathf.PI));
            yield return null;
        }
    }

    protected override IEnumerator Execute()
    {
        foreach (Pokemon target in TargetsIn(tailArea))
        {
            target.TakeDamageRPC(new DamageInfo(Actor.NetworkObjectId, 0.79f, 5, 220, DamageType.Physical));
            target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.5f, true, 0));
            target.ApplyKnockbackRPC(Direction, 12f);
            if (Upgraded && target.CurrentHp > 0 && Combat != null) Combat.ApplyTailMark(target);
        }
        yield return new WaitForSeconds(effectDuration);
    }
}
