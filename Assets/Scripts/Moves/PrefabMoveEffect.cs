using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>Shared server lifetime and targeting; all geometry lives in the prefab.</summary>
public abstract class PrefabMoveEffect : NetworkBehaviour
{
    [SerializeField] private GameObject graphics;
    [SerializeField] private float maximumLifetime = 15;
    protected PlayerManager Actor { get; private set; }
    protected Pokemon Target { get; private set; }
    protected bool Upgraded { get; private set; }
    protected Vector3 Direction { get; private set; }
    protected bool Arrived { get; private set; }
    private bool initialized, cleaned;
    private AsyncOperationHandle<GameObject> handle;
    private bool holdsAsset;
    public int CastSequence { get; set; }
    public AvailableMoves MoveId { get; set; }
    public event Action<PrefabMoveEffect> Completed;
    public event Action RecastReady;
    public virtual bool TryRecast(Vector3 direction) => false;
    protected void OfferRecast() { if (IsServer) RecastReady?.Invoke(); }
    private CombatActionLock actionLock;
    private WallCrossingTraversal traversal;
    private Coroutine driveRoutine;
    private bool phaseInterrupted;

    public void HoldAsset(AsyncOperationHandle<GameObject> asset) { handle = asset; holdsAsset = true; }
    public override void OnNetworkSpawn()
    {
        if (graphics != null) graphics.SetActive(false);
        if (IsServer) StartCoroutine(Watchdog());
    }

    public void Initialize(PlayerManager actor, Vector3 direction, Pokemon target, bool upgraded)
    {
        if (!IsServer || initialized) return;
        initialized = true;
        Actor = actor; Target = target; Direction = direction; Upgraded = upgraded;
        Actor.Pokemon.OnDeath += OnDeath;
        PresentRPC(actor.NetworkObjectId, target != null ? target.NetworkObjectId : 0, target != null, direction, upgraded);
        StartCoroutine(Run());
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PresentRPC(ulong actorId, ulong targetId, bool hasTarget, Vector3 direction, bool upgraded)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(actorId, out var actor)) return;
        Actor = actor.GetComponent<PlayerManager>();
        Target = hasTarget && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var target)
            ? target.GetComponent<Pokemon>() : null;
        Direction = direction; Upgraded = upgraded;
        if (graphics != null) graphics.SetActive(true);
        BeginPresentation();
    }

    protected virtual void BeginPresentation() { }
    protected virtual void Cleanup() { }
    protected bool CastInterrupted => phaseInterrupted || Actor == null || !Actor.IsSpawned || Actor.Pokemon.CurrentHp <= 0
        || Actor.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions);

    protected IEnumerator CastPause(float duration)
    {
        float until = Time.time + Mathf.Max(0, duration);
        while (Time.time < until)
        {
            if (CastInterrupted) { phaseInterrupted = true; yield break; }
            yield return null;
        }
    }
    protected void LockActions(bool movement = true) { SetActionLockRPC(true, movement); }
    protected void UnlockActions() { SetActionLockRPC(false, false); }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void SetActionLockRPC(bool active, bool movement)
    {
        actionLock?.Dispose(); actionLock = null;
        if (active && Actor != null) actionLock = new CombatActionLock(Actor, movement);
    }

    protected void PlayAnimation(string state, float duration = 0, float blendDuration = 0)
    {
        if (Actor == null) return;
        if (duration > 0) Actor.AnimationManager.PlayTimedAnimationRPC(state, duration, blendDuration);
        else Actor.AnimationManager.PlayAnimation(state, blendDuration);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    protected void FaceRPC(Vector3 direction)
    {
        if (Actor != null && direction.sqrMagnitude > 0.001f) Actor.transform.rotation = Quaternion.LookRotation(direction);
    }

    // One time-based path on server and owner, using the existing bounded,
    // cast-owned traversal. Other clients continue using NetworkTransform.
    protected void Drive(Vector3 destination, float duration, float bow = 0)
    {
        DriveRPC(Actor.transform.position, destination, duration, bow, NetworkManager.ServerTime.Time);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void DriveRPC(Vector3 origin, Vector3 destination, float duration, float bow, double began)
    {
        if (Actor == null || (!IsServer && !Actor.IsOwner)) return;
        driveRoutine = StartCoroutine(DrivePath(origin, destination, duration, bow, began));
    }

    private IEnumerator DrivePath(Vector3 origin, Vector3 destination, float duration, float bow, double began)
    {
        traversal?.End(); traversal = new WallCrossingTraversal(); traversal.Begin(Actor.PlayerMovement);
        Vector3 forward = destination - origin; forward.y = 0;
        Vector3 right = forward.sqrMagnitude > 0.001f ? Vector3.Cross(Vector3.up, forward.normalized) : Vector3.right;
        float t = 0;
        while (t < 1 && Actor != null && Actor.Pokemon.CurrentHp > 0 && !Actor.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions))
        {
            t = Mathf.Clamp01((float)(NetworkManager.ServerTime.Time - began) / Mathf.Max(0.01f, duration));
            Vector3 point = Vector3.Lerp(origin, destination, t) + right * (Mathf.Sin(t * Mathf.PI) * bow);
            traversal.Move(point - Actor.transform.position);
            yield return null;
        }
        if (Actor != null && Actor.IsSpawned && Actor.Pokemon.CurrentHp > 0) traversal.End(); else traversal.Cancel();
        traversal = null; driveRoutine = null;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void CancelDrivenMovementRPC()
    {
        if (driveRoutine != null) StopCoroutine(driveRoutine);
        driveRoutine = null; traversal?.Cancel(); traversal = null;
    }
    protected abstract IEnumerator Execute();
    private IEnumerator Run() { yield return Execute(); Despawn(); }
    private IEnumerator Watchdog() { yield return new WaitForSeconds(maximumLifetime); Despawn(); }
    private void OnDeath(DamageInfo damage) { CancelDrivenMovementRPC(); Despawn(); }
    protected virtual void Update()
    {
        if (IsServer && initialized && (Actor == null || !Actor.IsSpawned || Actor.Pokemon.CurrentHp <= 0)) Despawn();
    }

    protected bool CanHit(Pokemon pokemon, bool acquire = false)
    {
        return Actor != null && pokemon != null && pokemon.IsSpawned && pokemon.CurrentHp > 0
            && !pokemon.TeamMember.IsOnSameTeam(Actor.CurrentTeam)
            && !pokemon.HasStatusEffect(StatusType.Invincible) && !pokemon.HasStatusEffect(StatusType.Untargetable)
            && (!acquire || !pokemon.HasStatusEffect(StatusType.Invisible));
    }

    protected IEnumerator TravelTo(Pokemon recipient, float speed, Func<bool> valid = null)
    {
        Arrived = false;
        float until = Time.time + 3;
        while (recipient != null && recipient.IsSpawned && recipient.CurrentHp > 0 && Time.time < until
            && (valid == null || valid()))
        {
            Vector3 goal = recipient.transform.position + Vector3.up * 0.8f;
            transform.position = Vector3.MoveTowards(transform.position, goal, speed * Time.deltaTime);
            if ((transform.position - goal).sqrMagnitude < 0.04f) { Arrived = true; yield break; }
            yield return null;
        }
    }

    protected static void RefreshStat(Pokemon pokemon, StatChange change)
    {
        pokemon.RemoveAllStatChangeWithIDRPC(change.ID);
        pokemon.AddStatChange(change);
    }

    public void Despawn() { if (IsServer && IsSpawned) NetworkObject.Despawn(true); }
    public override void OnNetworkDespawn()
    {
        StopAllCoroutines();
        if (Actor != null && Actor.IsSpawned && Actor.Pokemon.CurrentHp > 0) traversal?.End(); else traversal?.Cancel();
        traversal = null; driveRoutine = null;
        actionLock?.Dispose(); actionLock = null;
        if (!cleaned) { cleaned = true; Cleanup(); }
        if (Actor != null) Actor.Pokemon.OnDeath -= OnDeath;
        if (IsServer) Completed?.Invoke(this);
        Completed = null; RecastReady = null;
        base.OnNetworkDespawn();
    }
    public override void OnDestroy()
    {
        if (holdsAsset && handle.IsValid()) Addressables.Release(handle);
        holdsAsset = false;
        base.OnDestroy();
    }
}
