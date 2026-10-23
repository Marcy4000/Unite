using System.Collections;
using UnityEngine;

public class FlygonBiteObject : FlygonMoveObject
{
    public override FlygonCast Kind => FlygonCast.Bite;
    protected override IEnumerator Execute()
    {
        yield return Wait(FlygonMoveRules.Windup(Kind), true);
        if (!Live) yield break;
        transform.SetPositionAndRotation(Actor.transform.position, Quaternion.LookRotation(Direction));
        SetPhase(1);
        foreach (Pokemon target in TargetsInArea()) target.TakeDamageRPC(FlygonMoveRules.Damage(Actor.NetworkObjectId, Kind));
        yield return Wait(0.27f);
    }
}
