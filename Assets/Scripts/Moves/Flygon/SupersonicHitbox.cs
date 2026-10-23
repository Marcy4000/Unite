using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SupersonicHitbox : FlygonMoveObject
{
    [SerializeField] private ParticleSystem particles;
    [SerializeField] private BoxCollider waveArea;
    public override FlygonCast Kind => FlygonCast.Supersonic;
    protected override void PresentPhase(byte value) { if (value == 1 && particles != null) particles.Play(true); }
    protected override IEnumerator Execute()
    {
        yield return Wait(FlygonMoveRules.Windup(Kind), true);
        if (!Live) yield break;
        SetPhase(1);
        var hit = new HashSet<Pokemon>();
        float start = Time.time, previous = 0;
        // The authored particles move at seven units/sec. Sweep the leading edge,
        // not the entire seven-unit telegraph, and do not move the emitter twice.
        while (Live && Time.time - start <= FlygonMoveRules.WaveRange / FlygonMoveRules.WaveSpeed + Time.deltaTime)
        {
            float distance = Mathf.Min(FlygonMoveRules.WaveRange, (Time.time - start) * FlygonMoveRules.WaveSpeed);
            Vector3 center = Origin + Direction * ((previous + distance) * 0.5f) + Vector3.up * 0.5f;
            float halfWidth = waveArea != null ? waveArea.size.x * 0.5f : FlygonMoveRules.WaveWidth * 0.5f;
            foreach (Collider collider in Physics.OverlapBox(center, new Vector3(halfWidth, 1, Mathf.Max(0.1f, (distance - previous) * 0.5f)), Quaternion.LookRotation(Direction)))
            {
                Pokemon target = collider.GetComponentInParent<Pokemon>();
                if (!CanControl(target) || !hit.Add(target)) continue;
                if (!target.HasStatusEffect(StatusType.HindranceResistance)) target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 1, true, EffectId));
                RefreshStat(target, Stat.Speed, FlygonMoveRules.SonicSlow(Upgraded), 4); // Three seconds remain after the stun.
            }
            previous = distance;
            if (distance >= FlygonMoveRules.WaveRange) break;
            yield return null;
        }
        yield return Wait(0.3f);
    }
}
