using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class TrevenantWoodHammerEffect : TrevenantMoveEffect
{
    [SerializeField] private TrevenantWoodHammerView hammerView;
    private bool offered, recast;
    private Vector3 secondDirection;
    private double recastExpiresAt;
    protected override void BeginPresentation()
    {
        base.BeginPresentation();
        if (hammerView != null) hammerView.Bind(Actor);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void SwingRPC(int stage, double began)
    { if (hammerView != null) hammerView.Swing(stage, began, TrevenantMoveRules.HammerWindup + TrevenantMoveRules.HammerRecovery); }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ImpactRPC(int stage, Vector3 origin, Vector3 direction, double began)
    { if (hammerView != null) hammerView.Impact(stage, origin, direction, began); }
    public override bool TryRecast(Vector3 direction)
    {
        if (!offered || recast || CastInterrupted || NetworkManager.ServerTime.Time >= recastExpiresAt) return false;
        offered = false; recast = true; secondDirection = direction; ClearRecastWindow(); return true;
    }
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction); transform.position = Actor.transform.position;
        Combat.PayCost(TrevenantMoveRules.HammerHealthCost, false);
        PlayContactAnimation(TrevenantMoveRules.Animation(Actor, "spell1a1"), TrevenantMoveRules.HammerWindup, TrevenantMoveRules.HammerRecovery, TrevenantMoveRules.HammerSweepContact);
        SwingRPC(1, NetworkManager.ServerTime.Time);
        yield return CastPause(TrevenantMoveRules.HammerWindup);
        if (CastInterrupted) yield break;
        bool hit = false; PhaseRPC(1); Physics.SyncTransforms();
        ImpactRPC(1, Actor.transform.position, Direction, NetworkManager.ServerTime.Time);
        foreach (var target in CombatTargeting.SectorTargets(Actor.transform.position, Direction, TrevenantMoveRules.HammerRange, TrevenantMoveRules.HammerAngle))
        {
            if (!CanHit(target)) continue;
            if (target.ResolveDamage(TrevenantMoveRules.Hammer(Actor.NetworkObjectId)) <= 0) continue;
            hit = true;
            if (Stun(target, 0.5f)) Displace(target, Actor.transform.position + Direction * TrevenantMoveRules.HammerGatherOffset, TrevenantMoveRules.HammerPullDuration);
        }
        yield return CastPause(TrevenantMoveRules.HammerRecovery); UnlockActions();
        if (CastInterrupted || !hit) yield break;
        recastExpiresAt = NetworkManager.ServerTime.Time + TrevenantMoveRules.HammerRecastWindow;
        Actor.MovesController.SetTrevenantHammerExpiry(recastExpiresAt);
        offered = true; OfferRecast();
        while (!recast && NetworkManager.ServerTime.Time < recastExpiresAt && TrevenantMoveRules.Living(Actor.Pokemon)) yield return null;
        offered = false; ClearRecastWindow();
        if (!recast || CastInterrupted) yield break;
        DirectionForSecond();
        Combat.PayCost(TrevenantMoveRules.HammerHealthCost, false);
        LockActions(); FaceRPC(secondDirection); PhaseRPC(0);
        PlayContactAnimation(TrevenantMoveRules.Animation(Actor, "spell1a2"), TrevenantMoveRules.HammerWindup, TrevenantMoveRules.HammerRecovery, TrevenantMoveRules.HammerSlamContact);
        SwingRPC(2, NetworkManager.ServerTime.Time);
        yield return CastPause(TrevenantMoveRules.HammerWindup);
        if (CastInterrupted) yield break;
        transform.position = Actor.transform.position; transform.rotation = Quaternion.LookRotation(secondDirection); PhaseRPC(2); Physics.SyncTransforms();
        ImpactRPC(2, Actor.transform.position + secondDirection * (TrevenantMoveRules.HammerSecondRange * 0.5f), secondDirection, NetworkManager.ServerTime.Time);
        var center = Actor.transform.position + secondDirection * (TrevenantMoveRules.HammerSecondRange * 0.5f) + Vector3.up;
        foreach (var target in CombatTargeting.BoxTargets(center, new Vector3(TrevenantMoveRules.HammerSecondWidth * 0.5f, 2, TrevenantMoveRules.HammerSecondRange * 0.5f), Quaternion.LookRotation(secondDirection)))
        {
            if (!CanHit(target)) continue;
            target.ResolveDamage(TrevenantMoveRules.Hammer(Actor.NetworkObjectId));
            Stun(target, TrevenantMoveRules.HammerStun(TrevenantMoveRules.HpFraction(Actor.Pokemon)));
            if (Upgraded && TrevenantMoveRules.Living(target)) RefreshStat(target, new StatChange(20, Stat.DamageDealtReduction, 3, true, false, true, EffectId));
        }
        yield return CastPause(TrevenantMoveRules.HammerRecovery); UnlockActions();
    }
    private void DirectionForSecond() { secondDirection = TrevenantMoveRules.Flat(secondDirection).normalized; }
    private void ClearRecastWindow()
    {
        if (recastExpiresAt <= 0) return;
        recastExpiresAt = 0;
        if (IsServer && Actor != null) Actor.MovesController.SetTrevenantHammerExpiry(0);
    }
    protected override void Cleanup() { offered = false; ClearRecastWindow(); if (hammerView != null) hammerView.Hide(); base.Cleanup(); }
}
