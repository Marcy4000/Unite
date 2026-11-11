using System.Collections;
using UnityEngine;

public class TrevenantPainSplitEffect : TrevenantMoveEffect
{
    [SerializeField] private Transform tether;
    private bool linked;
    private double expires;
    public bool LinkValid => linked && Actor != null && TrevenantMoveRules.Living(Actor.Pokemon) && TrevenantMoveRules.Living(Target)
        && NetworkManager.ServerTime.Time < expires && TrevenantMoveRules.InRange(Actor.transform.position, Target.transform.position, TrevenantMoveRules.PainBreakRange);
    public int RedirectDamage(int amount)
    {
        if (!LinkValid || Target.HasStatusEffect(StatusType.Invincible) || amount <= 0) return amount;
        int redirected = TrevenantMoveRules.RedirectAmount(amount, TrevenantMoveRules.HpFraction(Actor.Pokemon));
        var damage = DamageInfo.Tuned("trevenant.trevenantpainspliteffect.damage", new DamageInfo(Actor.NetworkObjectId, 0, 0, (short)Mathf.Min(short.MaxValue, redirected), DamageType.True)
            { source = DamageSource.Redirected, suppressOnHitEffects = true });
        // This source bypasses redirection hooks and damage modifiers, so two
        // opposing tethers cannot recurse or mitigate the same hit twice.
        int applied = Target.ResolveDamage(damage);
        return applied > 0 ? amount - redirected : amount;
    }
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction); transform.position = Actor.transform.position;
        PlayAnimation(TrevenantMoveRules.Animation(Actor, "spell2b"), TrevenantMoveRules.PainWindup + TrevenantMoveRules.PainRecovery);
        yield return CastPause(TrevenantMoveRules.PainWindup);
        if (CastInterrupted || !TrevenantMoveRules.Living(Target)) yield break;
        if (Combat.Link != null && Combat.Link != this) Combat.Link.Despawn();
        Combat.Link = this; linked = true;
        double began = NetworkManager.ServerTime.Time, next = began + TrevenantMoveRules.PainInterval;
        expires = began + (Upgraded ? TrevenantMoveRules.PainPlusDuration : TrevenantMoveRules.PainDuration);
        PhaseRPC(1);
        yield return CastPause(TrevenantMoveRules.PainRecovery); UnlockActions();
        int speedStep = -1;
        while (LinkValid)
        {
            double now = NetworkManager.ServerTime.Time;
            int step = Mathf.Min(4, Mathf.FloorToInt((float)(now - began) / 0.5f));
            if (step != speedStep)
            {
                speedStep = step;
                Actor.Pokemon.RemoveAllStatChangeWithIDRPC(EffectId);
                if (step < 4) Actor.Pokemon.AddStatChange(new StatChange(35f - step * 7.5f, Stat.Speed, 0.51f, true, true, true, EffectId).WithSource(Actor.NetworkObjectId));
            }
            while (now >= next && next < expires)
            {
                next += TrevenantMoveRules.PainInterval;
                if (CanHit(Target) && TrevenantMoveRules.HpFraction(Actor.Pokemon) < TrevenantMoveRules.HpFraction(Target))
                {
                    int dealt = Target.ResolveDamage(TrevenantMoveRules.Pain(Actor.NetworkObjectId));
                    if (dealt > 0) Actor.Pokemon.HealDamageRPC(Mathf.FloorToInt(dealt * TrevenantMoveRules.PainHealRatio), Actor.NetworkObjectId);
                }
            }
            yield return null;
        }
        linked = false; PhaseRPC(2);
    }
    protected override void Update()
    {
        base.Update();
        if (Actor == null) return;
        transform.position = Actor.transform.position;
        if (tether == null || Target == null) return;
        Vector3 delta = Target.transform.position - Actor.transform.position;
        tether.position = Actor.transform.position + Vector3.up * 0.8f + delta * 0.5f;
        if (delta.sqrMagnitude > 0.001f) tether.rotation = Quaternion.LookRotation(delta);
        tether.localScale = new Vector3(0.12f, 0.12f, delta.magnitude);
    }
    protected override void Cleanup()
    {
        linked = false;
        if (IsServer && Combat != null && Combat.Link == this) Combat.Link = null;
        base.Cleanup();
    }
}
