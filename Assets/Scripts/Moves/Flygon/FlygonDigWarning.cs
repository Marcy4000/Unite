using System.Collections;
using UnityEngine;

public class FlygonDigWarning : FlygonMoveObject
{
    private bool hasExit;
    private Vector3 exit;
    public override FlygonCast Kind => FlygonCast.Dig;
    public void SetExit(Vector3 point)
    {
        if (!IsServer || Phase != 2 || (point - Actor.transform.position).sqrMagnitude > 9 || !Actor.PlayerMovement.IsFlygonLandingValid(point)) return;
        hasExit = true; exit = point;
    }
    protected override IEnumerator Execute()
    {
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, 0, false, EffectId));
        yield return Wait(FlygonMoveRules.DigEnter);
        if (!Live) yield break;
        RefreshStat(Actor.Pokemon, Stat.Speed, 40, 0);
        SetPhase(1);
        float until = Time.time + FlygonMoveRules.DigDuration;
        while (Live && !EndRequested && Time.time < until) { transform.position = Actor.transform.position; yield return null; }
        if (!Live) yield break;
        SetPhase(2);
        float timeout = Time.time + 0.5f;
        while (Live && !hasExit && Time.time < timeout) yield return null;
        if (!Live) yield break;
        if (!hasExit) exit = Actor.PlayerMovement.IsFlygonLandingValid(Actor.transform.position) ? Actor.transform.position : Origin;
        transform.position = exit;
        Actor.PlayerMovement.FlygonExitPositionRPC(exit);
        Actor.Pokemon.RemoveStatusEffectWithID(EffectId);
        Actor.Pokemon.RemoveAllStatChangeWithIDRPC(EffectId);
        foreach (Pokemon target in TargetsInArea())
        {
            target.TakeDamageRPC(FlygonMoveRules.Damage(Actor.NetworkObjectId, Kind));
            if (CanControl(target)) RefreshStat(target, Stat.Speed, 15, 2);
        }
        yield return Wait(FlygonMoveRules.DigExit);
    }
    protected override void Cleanup()
    {
        if (!IsServer || Actor == null || !Actor.IsSpawned) return;
        Actor.Pokemon.RemoveStatusEffectWithID(EffectId);
        Actor.Pokemon.RemoveAllStatChangeWithIDRPC(EffectId);
    }
}
