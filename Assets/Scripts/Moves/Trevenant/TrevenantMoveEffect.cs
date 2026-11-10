using System.Collections;
using Unity.Netcode;
using UnityEngine;

public abstract class TrevenantMoveEffect : PrefabMoveEffect
{
    [SerializeField] private GameObject castVisual, activeVisual, impactVisual;
    protected GameObject ImpactVisual => impactVisual;
    protected TrevenantRuntime Combat => Actor != null ? Actor.MovesController.TrevenantCombat : null;
    private ushort effectId;
    private string animationState;
    private double animationEnds;
    protected ushort EffectId => effectId == 0 ? effectId = TrevenantEffectIds.Acquire() : effectId;
    protected new void PlayAnimation(string state, float duration = 0, float blendDuration = 0)
    {
        animationState = state; animationEnds = NetworkManager.ServerTime.Time + duration;
        base.PlayAnimation(state, duration, blendDuration);
    }
    protected new void UnlockActions()
    {
        base.UnlockActions(); RecoverPose();
    }
    protected void PlayContactAnimation(string state, float windup, float recovery, float contact)
    {
        animationState = state; animationEnds = NetworkManager.ServerTime.Time + windup + recovery;
        Actor.AnimationManager.PlayContactAnimationRPC(state, windup, recovery, contact);
    }
    private void RecoverPose()
    {
        if (!IsServer || Actor == null || !Actor.IsSpawned || string.IsNullOrEmpty(animationState)) return;
        Actor.StartCoroutine(Recover(Actor, animationState, Mathf.Max(0, (float)(animationEnds - NetworkManager.ServerTime.Time))));
        animationState = null;
    }
    private static IEnumerator Recover(PlayerManager actor, string expected, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        if (actor != null && actor.IsSpawned && TrevenantMoveRules.Living(actor.Pokemon) && !actor.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions))
            actor.AnimationManager.RecoverAnimationRPC(expected, TrevenantMoveRules.Animation(actor, "idle"), 0.08f);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    protected void PhaseRPC(int phase)
    {
        if (castVisual != null) castVisual.SetActive(phase == 0);
        if (activeVisual != null) activeVisual.SetActive(phase == 1);
        if (impactVisual != null) impactVisual.SetActive(phase == 2);
    }
    protected override void BeginPresentation()
    {
        if (castVisual != null) castVisual.SetActive(true);
        if (activeVisual != null) activeVisual.SetActive(false);
        if (impactVisual != null) impactVisual.SetActive(false);
    }
    protected void Slow(Pokemon target, short amount, float duration)
    {
        if (!CanHit(target) || target.HasStatusEffect(StatusType.Unstoppable)) return;
        RefreshStat(target, new StatChange(amount, Stat.Speed, duration, true, false, true, EffectId));
    }
    protected bool Stun(Pokemon target, float duration)
    {
        if (!CanHit(target)) return false;
        target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, duration, true, EffectId).WithSource(Actor.NetworkObjectId));
        return target.HasStatusEffect(EffectId);
    }
    protected void Displace(Pokemon target, Vector3 destination, float duration)
    {
        if (!CanHit(target) || target.HasStatusEffect(StatusType.Unstoppable) || target.HasStatusEffect(StatusType.HindranceResistance)) return;
        Vector3 delta = TrevenantMoveRules.Flat(destination - target.transform.position);
        if (target.TryGetComponent(out PlayerMovement player)) player.DisplaceRPC(delta.normalized, delta.magnitude, duration);
        else if (target.TryGetComponent(out WildPokemonAI wild)) wild.TryDisplace(delta.normalized, delta.magnitude, duration);
    }
    protected void Throw(Pokemon target, float duration)
    {
        if (Stun(target, duration)) target.ApplyKnockupRPC(TrevenantMoveRules.ThrowHeight, duration);
    }
    protected void SelfUnstoppable(float duration) => Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, duration, true, EffectId));
    protected void Haste(short amount, float duration) => Actor.Pokemon.AddStatChange(new StatChange(amount, Stat.Speed, duration, true, true, true, EffectId).WithSource(Actor.NetworkObjectId));
    protected void Visual(string name, Vector3 origin, Vector3 scale, float duration)
        => Actor.MovesController.ShowCombatVisualRPC(TrevenantMoveRules.Visuals + name, origin, Direction, scale, duration);
    protected override void Cleanup()
    {
        RecoverPose();
        if (IsServer && effectId != 0)
        {
            if (Actor != null && Actor.Pokemon != null && Actor.Pokemon.IsSpawned) Actor.Pokemon.RemoveStatusEffectWithID(effectId);
            TrevenantEffectIds.Release(effectId, 10);
        }
    }
}
