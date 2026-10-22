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
    protected abstract IEnumerator Execute();
    private IEnumerator Run() { yield return Execute(); Despawn(); }
    private IEnumerator Watchdog() { yield return new WaitForSeconds(maximumLifetime); Despawn(); }
    private void OnDeath(DamageInfo damage) { Despawn(); }
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
        if (!cleaned) { cleaned = true; Cleanup(); }
        if (Actor != null) Actor.Pokemon.OnDeath -= OnDeath;
        base.OnNetworkDespawn();
    }
    public override void OnDestroy()
    {
        if (holdsAsset && handle.IsValid()) Addressables.Release(handle);
        holdsAsset = false;
        base.OnDestroy();
    }
}
