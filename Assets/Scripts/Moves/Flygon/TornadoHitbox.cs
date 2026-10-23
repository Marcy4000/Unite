using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TornadoHitbox : FlygonMoveObject
{
    private struct Landing
    {
        public Pokemon Target;
        public float At;
        public DamageInfo Damage;
    }
    private readonly List<Landing> landings = new List<Landing>();
    public override FlygonCast Kind => FlygonCast.Unite;
    protected override void PresentPhase(byte value)
    {
        if (value == 5) RestoreActorAnimation();
    }
    private void RestoreActorAnimation()
    {
        if (Actor != null && Actor.AnimationManager != null) RestoreModelAnimation(Actor.AnimationManager.Animator);
    }
    public static bool RestoreModelAnimation(Animator animator)
    {
        if (animator == null || !animator.isInitialized || !animator.isActiveAndEnabled) return false;
        int tornado = Animator.StringToHash("Tornado");
        bool hidden = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == tornado
            || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == tornado);
        int recovery = Animator.StringToHash(FlygonMoveRules.UniteRecoveryAnimation);
        // A late despawn must not interrupt a new move, or force fog-hidden renderers on.
        if (!hidden || !animator.HasState(0, recovery)) return false;
        animator.Play(recovery, 0, 0);
        return true;
    }
    protected override IEnumerator Execute()
    {
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, FlygonMoveRules.UniteFormation, true, EffectId));
        yield return Wait(FlygonMoveRules.UniteFormation);
        if (!Live) yield break;
        Actor.Pokemon.RemoveStatusEffectWithID(EffectId);
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.15f), EffectId, 0, FlygonMoveRules.UniteDuration, true));
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.HindranceResistance, 0, false, EffectId));
        var lifted = new HashSet<Pokemon>();
        float start = Time.time, nextPull = start;
        int oldStage = -1;
        while (Live && !EndRequested && Time.time - start < FlygonMoveRules.UniteDuration)
        {
            transform.position = Actor.transform.position;
            int stage = FlygonMoveRules.TornadoStage(Time.time - start);
            if (stage != oldStage)
            {
                oldStage = stage; SetPhase((byte)(stage + 1));
                Actor.Pokemon.RemoveAllStatChangeWithIDRPC(EffectId);
                short speed = FlygonMoveRules.TornadoSpeed(stage);
                if (speed > 0) RefreshStat(Actor.Pokemon, Stat.Speed, speed, 0, true);
            }
            bool pull = Time.time >= nextPull;
            if (pull) nextPull = Time.time + 0.1f;
            foreach (Pokemon target in TargetsInArea())
            {
                bool controllable = CanControl(target) && !target.HasStatusEffect(StatusType.HindranceResistance);
                Vector3 offset = target.transform.position - transform.position; offset.y = 0;
                if (offset.magnitude <= FlygonMoveRules.UniteCore && lifted.Add(target))
                {
                    float duration = controllable ? FlygonMoveRules.TornadoLift(stage) : 0;
                    if (controllable)
                    {
                        target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, duration, true, EffectId));
                        target.ApplyKnockupRPC(2 * FlygonMoveRules.TornadoPower(stage), duration * 0.6f);
                    }
                    landings.Add(new Landing { Target = target, At = Time.time + duration * 0.6f, Damage = FlygonMoveRules.Damage(Actor.NetworkObjectId, Kind, stage) });
                }
                else if (controllable && pull && !lifted.Contains(target) && target.TryGetComponent(out PlayerMovement movement))
                    movement.FlygonDisplaceRPC(transform.position, 0.16f * FlygonMoveRules.TornadoPower(stage), true);
            }
            ResolveLandings();
            yield return null;
        }
        if (!Live) yield break;
        Actor.Pokemon.RemoveStatusEffectWithID(EffectId);
        Actor.Pokemon.RemoveShieldWithIDRPC(EffectId);
        Actor.Pokemon.RemoveAllStatChangeWithIDRPC(EffectId);
        RefreshStat(Actor.Pokemon, Stat.Speed, 25, 4, true);
        SetPhase(5); // Release controls immediately; already launched enemies still take landing damage.
        while (Live && landings.Count > 0) { ResolveLandings(); yield return null; }
    }
    private void ResolveLandings()
    {
        for (int i = landings.Count - 1; i >= 0; i--)
        {
            if (Time.time < landings[i].At) continue;
            Landing landing = landings[i]; landings.RemoveAt(i);
            if (CanHit(landing.Target)) landing.Target.TakeDamageRPC(landing.Damage);
        }
    }
    protected override void Cleanup()
    {
        // Runs on every peer, including death/cancellation and immediate despawns
        // where the final phase may not have reached the next replication tick.
        RestoreActorAnimation();
        if (!IsServer || Actor == null || !Actor.IsSpawned) return;
        Actor.Pokemon.RemoveStatusEffectWithID(EffectId);
        Actor.Pokemon.RemoveShieldWithIDRPC(EffectId);
        // Keep the intended post-Unite timed speed boost, but never an indefinite mode modifier.
        var remove = new List<StatChange>();
        foreach (var change in Actor.Pokemon.StatChanges) if (change.ID == EffectId && !change.IsTimed) remove.Add(change);
        foreach (var change in remove) Actor.Pokemon.RemoveStatChangeRPC(change);
        landings.Clear();
    }
    public override void OnDestroy()
    {
        RestoreActorAnimation();
        base.OnDestroy();
    }
}
