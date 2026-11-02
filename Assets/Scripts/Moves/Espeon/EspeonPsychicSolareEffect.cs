using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EspeonPsychicSolareEffect : EspeonMoveEffect
{
    [SerializeField] private SphereCollider area;
    [SerializeField] private Transform field;
    [SerializeField] private GameObject burst;
    private readonly HashSet<Pokemon> captured = new HashSet<Pokemon>();
    private readonly Dictionary<Pokemon, Lift> lifts = new Dictionary<Pokemon, Lift>();
    private CombatActionLock uniteLock;
    private ushort immunity;
    private float began;
    private struct Lift { public Transform model; public Vector3 origin; public float until; public ushort status; public float started; }
    protected override void BeginPresentation()
    {
        burst.SetActive(false); transform.position = Actor.transform.position;
        if (area != null && area.radius > 0) field.localScale = Vector3.one * (EspeonMoveRules.UniteRadius / area.radius);
    }
    protected override IEnumerator Execute()
    {
        immunity = EspeonEffectIds.Acquire(); began = Time.time;
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, EspeonMoveRules.UniteRecovery, true, immunity));
        LockRPC(true);
        PlayAnimation(EspeonMoveRules.Animation(Actor, "spellu"), EspeonMoveRules.UniteRecovery);
        if (area != null) area.radius = EspeonMoveRules.UniteRadius;
        while (Time.time - began < EspeonMoveRules.UniteExplodeAt)
        {
            if (Time.time - began >= EspeonMoveRules.UniteCaptureAt)
            {
                Physics.SyncTransforms();
                foreach (var target in CombatTargeting.SphereTargets(Actor.transform.position + Vector3.up * 0.8f, EspeonMoveRules.UniteRadius))
                {
                    if (!CanHit(target) || captured.Contains(target)) continue;
                    float remaining = Mathf.Max(0.01f, EspeonMoveRules.UniteExplodeAt - (Time.time - began));
                    if (Stun(target, remaining)) { captured.Add(target); LiftRPC(target.NetworkObjectId, remaining, StunId); }
                }
            }
            yield return null;
        }
        foreach (var target in captured) if (target != null && target.IsSpawned) target.RemoveStatusEffectWithID(StunId);
        foreach (var target in CombatTargeting.SphereTargets(Actor.transform.position + Vector3.up * 0.8f, EspeonMoveRules.UniteRadius))
        {
            if (Hit(target, EspeonMoveRules.Unite(Actor.NetworkObjectId)) <= 0) continue;
            EspeonMoveRules.Shove(target, target.transform.position - Actor.transform.position);
        }
        ExplodeRPC();
        Actor.Pokemon.AddStatChange(new StatChange(30, Stat.Speed, 6, true, true, true, immunity));
        Actor.Pokemon.AddStatChange(new StatChange(30, Stat.Cdr, 6, true, true, false, immunity));
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.2f), immunity, 0, 6, true));
        yield return new WaitForSeconds(Mathf.Max(0, EspeonMoveRules.UniteRecovery - EspeonMoveRules.UniteExplodeAt));
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void LockRPC(bool active)
    {
        uniteLock?.Dispose(); uniteLock = null;
        // Eject Button stays available during the stationary, unstoppable cast.
        if (active && Actor != null) uniteLock = new CombatActionLock(Actor, true, lockBattleItems: false);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void LiftRPC(ulong targetId, float duration, ushort status)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj) || !obj.TryGetComponent(out Pokemon victim)) return;
        Transform model = victim.ActiveModel != null ? victim.ActiveModel.transform : null;
        if (model == null && obj.TryGetComponent(out PlayerManager player) && player.AnimationManager.Animator != null)
            model = player.AnimationManager.Animator.transform;
        if (model == null || model == obj.transform) return;
        lifts[victim] = new Lift { model = model, origin = model.localPosition, until = Time.time + duration, status = status, started = Time.time };
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ExplodeRPC() { field.gameObject.SetActive(false); burst.SetActive(true); RestoreLifts(); }
    protected override void Update()
    {
        base.Update();
        if (Actor != null) transform.position = Actor.transform.position;
        foreach (var pair in new List<KeyValuePair<Pokemon, Lift>>(lifts))
        {
            var lift = pair.Value;
            if (lift.model == null) { lifts.Remove(pair.Key); continue; }
            if (!EspeonMoveRules.Living(pair.Key) || Time.time >= lift.until || (Time.time - lift.started > 0.1f && !pair.Key.HasStatusEffect(lift.status)))
            { lift.model.localPosition = lift.origin; lifts.Remove(pair.Key); continue; }
            lift.model.localPosition = lift.origin + Vector3.up * EspeonMoveRules.LiftHeight * Mathf.Clamp01((Time.time - lift.started) / 0.12f);
        }
    }
    private void RestoreLifts() { foreach (var lift in lifts.Values) if (lift.model != null) lift.model.localPosition = lift.origin; lifts.Clear(); }
    protected override void Cleanup()
    {
        uniteLock?.Dispose(); uniteLock = null; RestoreLifts();
        if (IsServer)
        {
            foreach (var target in captured) if (target != null && target.IsSpawned) target.RemoveStatusEffectWithID(StunId);
            if (Actor != null && Actor.Pokemon.IsSpawned) Actor.Pokemon.RemoveStatusEffectWithID(immunity);
            if (immunity != 0) EspeonEffectIds.ReleaseAfter(immunity, 6);
        }
        base.Cleanup();
    }
}
