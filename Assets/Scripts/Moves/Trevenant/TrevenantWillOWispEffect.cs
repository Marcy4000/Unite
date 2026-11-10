using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TrevenantWillOWispEffect : TrevenantMoveEffect
{
    [SerializeField] private Transform[] flames;
    private bool launched;
    private Vector3 flightOrigin, flightDirection;
    private double flightBegan;
    protected override void BeginPresentation()
    {
        base.BeginPresentation();
        if (flames != null)
            for (int i = 0; i < flames.Length; i++) if (flames[i] != null) flames[i].localPosition = new Vector3((i - 1) * TrevenantMoveRules.WispSpacing, 0.8f, 0);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void LaunchRPC(Vector3 origin, Vector3 direction, double began)
    {
        flightOrigin = origin; flightDirection = direction; flightBegan = began; launched = true;
        PresentFlames();
    }
    private void LateUpdate()
    {
        // Resolve after server movement / NetworkTransform so moving the
        // effect root cannot offset the independent flame trajectories.
        if (launched) PresentFlames();
    }
    private void PresentFlames()
    {
        if (flames == null || NetworkManager == null) return;
        float travelled = Mathf.Clamp((float)(NetworkManager.ServerTime.Time - flightBegan) * TrevenantMoveRules.WispSpeed, 0, TrevenantMoveRules.WispRange);
        for (int i = 0; i < flames.Length; i++)
            if (flames[i] != null) flames[i].SetPositionAndRotation(TrevenantMoveRules.WispPosition(flightOrigin, flightDirection, i, travelled),
                Quaternion.LookRotation(TrevenantMoveRules.WispDirection(flightDirection, i)));
    }
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction); transform.position = Actor.transform.position;
        PlayAnimation(TrevenantMoveRules.Animation(Actor, "spell1"), TrevenantMoveRules.WispWindup + TrevenantMoveRules.WispRecovery);
        yield return CastPause(TrevenantMoveRules.WispWindup);
        if (CastInterrupted) yield break;
        PhaseRPC(1); UnlockActions();
        var hits = new HashSet<Pokemon>();
        var flameHits = new[] { new HashSet<Pokemon>(), new HashSet<Pokemon>(), new HashSet<Pokemon>() };
        Vector3 origin = Actor.transform.position;
        double began = NetworkManager.ServerTime.Time;
        LaunchRPC(origin, Direction, began);
        float travelled = 0;
        while (travelled < TrevenantMoveRules.WispRange)
        {
            float next = Mathf.Clamp((float)(NetworkManager.ServerTime.Time - began) * TrevenantMoveRules.WispSpeed, travelled, TrevenantMoveRules.WispRange);
            Physics.SyncTransforms();
            for (int i = 0; i < 3; i++)
            {
                foreach (var target in CombatTargeting.SweptSphereTargets(TrevenantMoveRules.WispPosition(origin, Direction, i, travelled),
                    TrevenantMoveRules.WispPosition(origin, Direction, i, next), TrevenantMoveRules.WispRadius))
                {
                    if (!CanHit(target) || !flameHits[i].Add(target)) continue;
                    bool first = hits.Add(target);
                    if (target.ResolveDamage(TrevenantMoveRules.Wisp(Actor.NetworkObjectId, first)) > 0 && TrevenantMoveRules.Living(target))
                    {
                        target.AddStatusEffect(StatusEffect.DamageOverTime(StatusType.Burned, TrevenantMoveRules.Burn(Actor.NetworkObjectId),
                            TrevenantMoveRules.BurnDuration, TrevenantMoveRules.BurnInterval, EffectId));
                        RefreshStat(target, new StatChange(10, Stat.Attack, TrevenantMoveRules.BurnDuration, true, false, true, EffectId));
                        // RefreshStat removes all stats with an ID; add the paired stat after the refresh.
                        target.AddStatChange(new StatChange(5, Stat.SpAttack, TrevenantMoveRules.BurnDuration, true, false, true, EffectId).WithSource(Actor.NetworkObjectId));
                    }
                }
            }
            travelled = next; transform.position = origin + Direction * travelled;
            yield return null;
        }
        PhaseRPC(2); yield return new WaitForSeconds(TrevenantMoveRules.WispRecovery);
    }
}
