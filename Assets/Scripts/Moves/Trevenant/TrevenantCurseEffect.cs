using System.Collections;
using UnityEngine;

public class TrevenantCurseEffect : TrevenantMoveEffect
{
    [SerializeField] private Transform zone;
    private int events;
    private bool detonate, active;
    public float Gauge => TrevenantMoveRules.GaugeFraction(events);
    public void RecordDamageEvent() { if (active) events = Mathf.Min(Mathf.Max(1, TrevenantMoveRules.CurseEventsToFill), events + 1); }
    public override bool TryRecast(Vector3 direction) { if (!active || detonate || CastInterrupted) return false; detonate = true; return true; }
    protected override void BeginPresentation()
    {
        base.BeginPresentation();
        if (zone != null) zone.localScale = Vector3.one * TrevenantMoveRules.CurseArea(Upgraded);
        if (ImpactVisual != null) ImpactVisual.transform.localScale = Vector3.one * (TrevenantMoveRules.CurseArea(Upgraded) / TrevenantMoveRules.AuthoredCurseRadius);
    }
    protected override IEnumerator Execute()
    {
        LockActions(); transform.position = Actor.transform.position;
        PlayAnimation(TrevenantMoveRules.Animation(Actor, "spell1b1"), TrevenantMoveRules.CurseWindup);
        yield return CastPause(TrevenantMoveRules.CurseWindup);
        if (CastInterrupted) yield break;
        double began = NetworkManager.ServerTime.Time, next = began + TrevenantMoveRules.CurseInterval, cost = began + TrevenantMoveRules.CurseHealthInterval;
        active = true; Combat.Curse = this;
        Actor.MovesController.SetTrevenantCurseExpiry(began + TrevenantMoveRules.CurseDuration);
        PhaseRPC(1); UnlockActions(); OfferRecast();
        while (TrevenantMoveRules.Living(Actor.Pokemon))
        {
            double now = NetworkManager.ServerTime.Time;
            // Process final scheduled ticks before automatic detonation.
            while (!detonate && next <= began + TrevenantMoveRules.CurseDuration + 0.001 && now >= next)
            {
                TickDamage(false); next += TrevenantMoveRules.CurseInterval;
            }
            while (!detonate && cost <= began + TrevenantMoveRules.CurseDuration + 0.001 && now >= cost)
            { Combat.PayCost(TrevenantMoveRules.CurseHealthCost, true); cost += TrevenantMoveRules.CurseHealthInterval; }
            if (detonate || now >= began + TrevenantMoveRules.CurseDuration) break;
            yield return null;
        }
        if (!TrevenantMoveRules.Living(Actor.Pokemon)) yield break;
        bool full = Gauge >= 1; active = false;
        Actor.MovesController.SetTrevenantCurseExpiry(0);
        LockActions(); PhaseRPC(2); PlayAnimation(TrevenantMoveRules.Animation(Actor, "spell1b2"), TrevenantMoveRules.CurseRecovery);
        TickDamage(true, full);
        if (full) Actor.MovesController.ResetTrevenantSecondMove();
        yield return new WaitForSeconds(TrevenantMoveRules.CurseRecovery); UnlockActions();
    }
    private void TickDamage(bool burst, bool full = false)
    {
        Physics.SyncTransforms();
        foreach (var target in CombatTargeting.SphereTargets(Actor.transform.position + Vector3.up * 0.8f, TrevenantMoveRules.CurseArea(Upgraded)))
        {
            if (!CanHit(target)) continue;
            target.ResolveDamage(TrevenantMoveRules.Curse(Actor.NetworkObjectId, burst));
            if (burst && full) Stun(target, 0.6f);
            else if (!burst) Slow(target, TrevenantMoveRules.CurseSlow(TrevenantMoveRules.HpFraction(Actor.Pokemon)), 1);
        }
    }
    protected override void Update() { base.Update(); if (Actor != null) transform.position = Actor.transform.position; }
    protected override void Cleanup()
    {
        if (IsServer && Combat != null && Combat.Curse == this)
        { Combat.Curse = null; Actor.MovesController.SetTrevenantCurseExpiry(0); }
        active = false; base.Cleanup();
    }
}
