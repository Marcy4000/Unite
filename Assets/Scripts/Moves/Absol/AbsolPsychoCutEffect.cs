using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class AbsolPsychoCutEffect : PrefabMoveEffect
{
    [SerializeField] private GameObject blade;
    [SerializeField] private StackMarkerView markerPrefab;
    private readonly Dictionary<Pokemon, Mark> marks = new Dictionary<Pokemon, Mark>();
    private readonly Dictionary<ulong, StackMarkerView> views = new Dictionary<ulong, StackMarkerView>();
    private ushort MarkId => (ushort)(40000 + NetworkObjectId % 10000);
    private ushort SlowId => (ushort)(50000 + Actor.NetworkObjectId % 10000);
    private sealed class Mark { public int remaining = 3; public float until; }
    private bool castFinished;
    protected override void BeginPresentation() { if (blade != null) blade.SetActive(false); }

    protected override IEnumerator Execute()
    {
        Actor.Pokemon.OnDamageApplied += Consume;
        LockActions();
        FaceRPC(Direction); PlayAnimation(AbsolMoveRules.PsychoAnimation, AbsolMoveRules.PsychoAnimationDuration, AbsolMoveRules.MoveAnimationBlend);
        // The cast owns movement only through its animation/recovery, never
        // through the projectile flight or the five-second mark lifetime.
        StartCoroutine(ReleaseCastLock());
        yield return CastPause(AbsolMoveRules.PsychoWindup);
        if (CastInterrupted) yield break;
        ShowBladeRPC();
        Vector3 origin = Actor.transform.position + Vector3.up * 0.8f, previous = origin;
        float began = Time.time;
        var hit = new HashSet<Pokemon>();
        while (Time.time - began < AbsolMoveRules.PsychoRange / AbsolMoveRules.PsychoSpeed)
        {
            float distance = Mathf.Min(AbsolMoveRules.PsychoRange, (Time.time - began) * AbsolMoveRules.PsychoSpeed);
            Vector3 point = origin + Direction * distance;
            transform.position = point;
            foreach (var target in CombatTargeting.SweptSphereTargets(previous, point, AbsolMoveRules.PsychoWidth * 0.5f))
            {
                if (!CanHit(target) || !hit.Add(target)) continue;
                int dealt = target.ResolveDamage(AbsolMoveRules.PsychoDamage(Actor.NetworkObjectId));
                if (dealt <= 0 || target.CurrentHp <= 0) continue;
                foreach (var effect in Actor.MovesController.ActivePrefabEffects)
                    if (effect is AbsolPsychoCutEffect other && other != this) other.RemoveMark(target);
                marks[target] = new Mark { until = Time.time + AbsolMoveRules.MarkDuration };
                target.AddStatusEffect(new StatusEffect(StatusType.Scriptable, AbsolMoveRules.MarkDuration, true, MarkId));
                target.AddSteppedDecayingStatRPC(new StatChange(90, Stat.Speed, 2, true, false, true, SlowId), 25, 0.5f);
                ShowMarksRPC(target.NetworkObjectId, 3);
                UpdateSpeed();
            }
            previous = point; yield return null;
        }
        HideBladeRPC();
        while (marks.Count > 0 || !castFinished)
        {
            foreach (var target in new List<Pokemon>(marks.Keys))
                if (target == null || !target.IsSpawned || target.CurrentHp <= 0 || Time.time >= marks[target].until || !target.HasStatusEffect(MarkId)) RemoveMark(target);
            UpdateSpeed();
            yield return null;
        }
    }

    private IEnumerator ReleaseCastLock()
    {
        yield return CastPause(Mathf.Max(AbsolMoveRules.PsychoWindup, AbsolMoveRules.PsychoAnimationDuration) + AbsolMoveRules.PsychoRecovery);
        UnlockActions();
        castFinished = true;
        if (!CastInterrupted) Actor.AnimationManager.RecoverAnimationRPC(AbsolMoveRules.PsychoAnimation, AbsolMoveRules.Idle, AbsolMoveRules.RecoveryAnimationBlend);
    }

    private void Consume(Pokemon target, DamageInfo damage, int dealt)
    {
        if (dealt <= 0 || !AbsolMoveRules.CanConsumeMark(damage) || !marks.TryGetValue(target, out var mark)) return;
        if (!CanHit(target) || Time.time >= mark.until || !target.HasStatusEffect(MarkId)) { RemoveMark(target); return; }
        int remaining = mark.remaining--;
        if (mark.remaining == 0) RemoveMark(target); else ShowMarksRPC(target.NetworkObjectId, mark.remaining);
        target.ResolveDamage(AbsolMoveRules.MarkDamage(Actor.NetworkObjectId, remaining));
    }

    public void RemoveMark(Pokemon target)
    {
        if (ReferenceEquals(target, null) || !marks.Remove(target)) return;
        if (target == null) { UpdateSpeed(); return; }
        if (target.IsSpawned) target.RemoveStatusEffectWithID(MarkId);
        // Despawn already removes each peer's views in Cleanup; this effect
        // cannot send another RPC once OnNetworkDespawn has begun.
        if (IsSpawned) ShowMarksRPC(target.NetworkObjectId, 0);
        UpdateSpeed();
    }

    private void UpdateSpeed()
    {
        if (!IsServer || Actor == null || !Actor.Pokemon.IsSpawned) return;
        bool any = Upgraded && marks.Count > 0;
        foreach (var effect in Actor.MovesController.ActivePrefabEffects)
            if (effect is AbsolPsychoCutEffect other && other != this && other.Upgraded && other.marks.Count > 0) any = true;
        bool present = Actor.Pokemon.HasStatChangeWithID(AbsolMoveRules.PsychoSpeedId);
        if (any && !present) Actor.Pokemon.AddStatChange(new StatChange(20, Stat.Speed, 0, false, true, true, AbsolMoveRules.PsychoSpeedId));
        else if (!any && present) Actor.Pokemon.RemoveAllStatChangeWithIDRPC(AbsolMoveRules.PsychoSpeedId);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void HideBladeRPC() { if (blade != null) blade.SetActive(false); }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ShowBladeRPC() { if (blade != null) blade.SetActive(true); }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ShowMarksRPC(ulong targetId, int count)
    {
        if (!IsClient || Actor == null || !Actor.IsOwner) return;
        if (count == 0)
        {
            if (views.TryGetValue(targetId, out var old) && old != null) Destroy(old.gameObject);
            views.Remove(targetId); return;
        }
        if (!views.TryGetValue(targetId, out var view) || view == null)
        {
            if (markerPrefab == null || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var target)) return;
            view = Instantiate(markerPrefab, target.transform); views[targetId] = view;
        }
        view.SetCount(count);
    }

    protected override void Cleanup()
    {
        if (IsServer && Actor != null)
        {
            Actor.Pokemon.OnDamageApplied -= Consume;
            foreach (var target in new List<Pokemon>(marks.Keys)) RemoveMark(target);
            UpdateSpeed();
        }
        foreach (var view in views.Values) if (view != null) Destroy(view.gameObject); views.Clear();
    }
}
