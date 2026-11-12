using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public abstract class LucarioMoveEffect : PrefabMoveEffect
{
    [SerializeField] private GameObject castVisual, activeVisual, impactVisual;
    [SerializeField] private Transform handVisual;
    private Transform handAnchor;
    protected LucarioRuntime Combat => Actor != null ? Actor.MovesController.LucarioCombat : null;
    protected bool FollowActor = true;
    private ushort effectId;
    private string animationState;
    private double animationEnds;
    protected ushort EffectId => effectId == 0 ? effectId = LucarioEffectIds.Acquire() : effectId;
    protected void PlayAnimation(string clip, float duration)
    {
        animationState = LucarioMoveRules.Animation(clip); animationEnds = NetworkManager.ServerTime.Time + duration;
        base.PlayAnimation(animationState, duration, 0.06f);
    }
    protected new void UnlockActions() { base.UnlockActions(); RecoverPose(); }
    private void RecoverPose()
    {
        if (!IsServer || Actor == null || !Actor.IsSpawned || string.IsNullOrEmpty(animationState)) return;
        Actor.StartCoroutine(Recover(Actor, animationState, Mathf.Max(0, (float)(animationEnds - NetworkManager.ServerTime.Time)))); animationState = null;
    }
    private static IEnumerator Recover(PlayerManager actor, string state, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);
        if (actor != null && actor.IsSpawned && LucarioMoveRules.Living(actor.Pokemon) && !actor.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions))
            actor.AnimationManager.RecoverAnimationRPC(state, LucarioMoveRules.Animation("idle"), 0.08f);
    }
    protected override void BeginPresentation()
    {
        SetPhase(0);
        if (Actor != null && handVisual != null)
            foreach (var t in Actor.GetComponentsInChildren<Transform>(true)) if (t.name == "Bip001 R Hand") { handAnchor = t; break; }
    }
    private void LateUpdate() { if (handVisual != null && handAnchor != null) handVisual.SetPositionAndRotation(handAnchor.position, handAnchor.rotation); }
    private void SetPhase(int phase)
    {
        if (castVisual != null) castVisual.SetActive(phase == 0);
        if (activeVisual != null) activeVisual.SetActive(phase == 1);
        if (impactVisual != null) impactVisual.SetActive(phase == 2);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    protected void PhaseRPC(int phase) { SetPhase(phase); }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    protected void DetachRPC(Vector3 position)
    { FollowActor = false; transform.SetPositionAndRotation(position, Quaternion.LookRotation(Direction)); }
    protected override void Update()
    {
        base.Update(); if (FollowActor && Actor != null) transform.SetPositionAndRotation(Actor.transform.position, Actor.transform.rotation);
    }
    protected IEnumerable<Pokemon> Box(float width, float length, float offset)
    {
        Physics.SyncTransforms();
        return CombatTargeting.BoxTargets(Actor.transform.position + Vector3.up + Direction * offset, new Vector3(width, 2.5f, length) * 0.5f, Quaternion.LookRotation(Direction));
    }
    protected static IEnumerable<Pokemon> Sweep(Vector3 from, Vector3 to, Vector3 facing, float width)
    {
        Vector3 delta = LucarioMoveRules.Flat(to - from);
        return CombatTargeting.BoxTargets((from + to) * 0.5f + Vector3.up,
            new Vector3(width * 0.5f, 1.25f, Mathf.Max(0.05f, delta.magnitude * 0.5f)), Quaternion.LookRotation(delta.sqrMagnitude > 0.0001f ? delta : facing));
    }
    protected IEnumerator Dash(Vector3 direction, float distance, float speed, float width, System.Action<Pokemon> hit)
    {
        Vector3 previous = Actor.transform.position;
        float duration = distance / Mathf.Max(1, speed);
        Drive(previous + direction * distance, duration);
        double until = NetworkManager.ServerTime.Time + duration;
        var hits = new HashSet<Pokemon>();
        while (!CastInterrupted)
        {
            Physics.SyncTransforms();
            var candidates = new List<Pokemon>(Sweep(previous, Actor.transform.position, direction, width));
            // Stable path order matters when only the first marked victim may supply a reset.
            candidates.Sort((a, b) => Vector3.Dot(a.transform.position - previous, direction).CompareTo(Vector3.Dot(b.transform.position - previous, direction)));
            foreach (var target in candidates) if (CanHit(target) && hits.Add(target)) hit(target);
            previous = Actor.transform.position;
            if (NetworkManager.ServerTime.Time >= until) break;
            yield return null;
        }
    }
    protected void Shove(Pokemon target, Vector3 direction, float distance, float duration)
    {
        if (!CanHit(target)) return;
        target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, duration, true, EffectId).WithSource(Actor.NetworkObjectId));
        if (target.HasStatusEffect(StatusType.Unstoppable) || target.HasStatusEffect(StatusType.HindranceResistance)) return;
        if (target.TryGetComponent(out PlayerMovement player)) player.DisplaceRPC(direction, distance, duration);
        else if (target.TryGetComponent(out WildPokemonAI wild)) wild.TryDisplace(direction, distance, duration);
    }
    protected void Unstoppable(float duration) => Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, duration, true, EffectId));
    protected void Visual(string name, Vector3 position, float radius, float duration = 0.2f)
        => Actor.MovesController.ShowCombatVisualRPC(LucarioMoveRules.Visuals + name, position, Direction, Vector3.one * radius, duration);
    protected override void Cleanup()
    {
        RecoverPose();
        if (IsServer && effectId != 0)
        {
            if (Actor != null && Actor.Pokemon != null && Actor.Pokemon.IsSpawned)
            { Actor.Pokemon.RemoveStatusEffectWithID(effectId); Actor.Pokemon.RemoveAllStatChangeWithIDRPC(effectId); }
            LucarioEffectIds.Release(effectId, 10);
        }
    }
}
