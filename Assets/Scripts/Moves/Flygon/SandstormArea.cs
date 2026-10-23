using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SandstormArea : FlygonMoveObject
{
    public override FlygonCast Kind => FlygonCast.Sandstorm;
    protected override IEnumerator Execute()
    {
        transform.position = Destination;
        yield return Wait(FlygonMoveRules.Windup(Kind), true);
        if (!Live) yield break;
        SetPhase(1);
        var blinded = new HashSet<Pokemon>();
        bool speedApplied = false;
        float start = Time.time, tick = start + FlygonMoveRules.StormInterval;
        while (Live && Time.time - start < FlygonMoveRules.StormDuration)
        {
            Vector3 offset = Actor.transform.position - transform.position; offset.y = 0;
            bool inside = offset.sqrMagnitude <= FlygonMoveRules.StormRadius * FlygonMoveRules.StormRadius;
            if (Upgraded && inside != speedApplied)
            {
                speedApplied = inside;
                if (inside) RefreshStat(Actor.Pokemon, Stat.Speed, 15, 0, true);
                else Actor.Pokemon.RemoveAllStatChangeWithIDRPC(EffectId);
            }
            if (Time.time >= tick)
            {
                tick += FlygonMoveRules.StormInterval;
                foreach (Pokemon target in TargetsInArea())
                {
                    target.TakeDamageRPC(FlygonMoveRules.Damage(Actor.NetworkObjectId, Kind));
                    if (CanControl(target) && blinded.Add(target)) target.AddStatusEffect(new StatusEffect(StatusType.VisionObscuring, 3, true, EffectId));
                }
            }
            yield return null;
        }
    }
    protected override void Cleanup()
    {
        if (IsServer && Actor != null && Actor.IsSpawned) Actor.Pokemon.RemoveAllStatChangeWithIDRPC(EffectId);
    }
}
