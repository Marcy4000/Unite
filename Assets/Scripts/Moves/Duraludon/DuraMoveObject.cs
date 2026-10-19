using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>Common lifetime/targeting for Duraludon's authored move prefabs.</summary>
public abstract class DuraMoveObject : NetworkBehaviour
{
    [SerializeField] private GameObject graphics;
    [SerializeField, Min(1)] private float maximumLifetime = 75f;

    protected PlayerManager Actor { get; private set; }
    protected Vector3 Origin { get; private set; }
    protected Vector3 Direction { get; private set; }
    protected Vector3 Destination { get; private set; }
    protected float Power { get; private set; }
    protected bool Upgraded { get; private set; }
    protected DuraBoostedObj Combat => Actor != null && DuraBoostedObj.TryGetCombat(Actor.NetworkObjectId, out var combat) ? combat : null;
    private bool initialized;
    private AsyncOperationHandle<GameObject> assetHandle;
    private bool holdsAsset;

    public void HoldAsset(AsyncOperationHandle<GameObject> handle)
    {
        assetHandle = handle;
        holdsAsset = true;
    }

    public override void OnNetworkSpawn()
    {
        graphics.SetActive(false);
        if (IsServer) StartCoroutine(Watchdog());
    }

    private IEnumerator Watchdog()
    {
        yield return new WaitForSeconds(maximumLifetime);
        Despawn();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Server)]
    public void InitializeRPC(ulong actorId, Vector3 origin, Vector3 direction, float power, bool upgraded, Vector3 destination)
    {
        if (initialized) return;
        if (!Bind(actorId, origin, direction, power, upgraded, destination) || Actor.Pokemon.CurrentHp <= 0)
        {
            Despawn();
            return;
        }
        initialized = true;
        Actor.Pokemon.OnDeath += OnActorDeath;
        BeginPresentationRPC(actorId, origin, direction, power, upgraded, destination);
        StartCoroutine(Run());
    }

    private bool Bind(ulong actorId, Vector3 origin, Vector3 direction, float power, bool upgraded, Vector3 destination)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(actorId, out var actorObject)
            || !actorObject.TryGetComponent(out PlayerManager actor)) return false;
        Actor = actor;
        Origin = origin;
        direction.y = 0;
        Direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        Destination = destination;
        Power = Mathf.Clamp01(power);
        Upgraded = upgraded;
        transform.SetPositionAndRotation(origin, Quaternion.LookRotation(Direction));
        Configure();
        return true;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void BeginPresentationRPC(ulong actorId, Vector3 origin, Vector3 direction, float power, bool upgraded, Vector3 destination)
    {
        if (!Bind(actorId, origin, direction, power, upgraded, destination)) return;
        if (IsClient)
        {
            graphics.SetActive(true);
            BeginPresentation();
        }
    }

    protected virtual void Configure() { }
    protected virtual void BeginPresentation() { }
    protected virtual void EndPresentation() { }
    protected abstract IEnumerator Execute();

    private IEnumerator Run()
    {
        yield return Execute();
        Despawn();
    }

    protected virtual void Update()
    {
        if (IsServer && initialized && (Actor == null || !Actor.IsSpawned)) Despawn();
    }

    private void OnActorDeath(DamageInfo damage) { Despawn(); }

    public void Despawn()
    {
        if (IsServer && IsSpawned) NetworkObject.Despawn(true);
    }

    protected bool CanHit(Pokemon target)
    {
        return Actor != null && target != null && target.CurrentHp > 0
            && !target.TeamMember.IsOnSameTeam(Actor.CurrentTeam)
            && !target.HasStatusEffect(StatusType.Invincible)
            && !target.HasStatusEffect(StatusType.Untargetable);
    }

    protected List<Pokemon> TargetsIn(SphereCollider area)
    {
        Vector3 scale = area.transform.lossyScale;
        float radius = area.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        return Targets(Physics.OverlapSphere(area.transform.TransformPoint(area.center), radius));
    }

    protected List<Pokemon> TargetsIn(BoxCollider area)
    {
        Vector3 scale = area.transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return Targets(Physics.OverlapBox(area.transform.TransformPoint(area.center), Vector3.Scale(area.size * 0.5f, scale), area.transform.rotation));
    }

    private List<Pokemon> Targets(Collider[] colliders)
    {
        var targets = new HashSet<Pokemon>();
        foreach (Collider hit in colliders)
            if (hit.TryGetComponent(out Pokemon target) && CanHit(target)) targets.Add(target);
        return new List<Pokemon>(targets);
    }

    protected void Slow(Pokemon target, short amount, float duration, ushort id)
    {
        target.RemoveStatChangeWithIDRPC(id);
        target.AddStatChange(new StatChange(amount, Stat.Speed, duration, true, false, true, id));
    }

    protected void AddRefreshingShield(ushort id, int amount, int maximum, float duration)
    {
        int remaining = Actor.Pokemon.GetShieldWithID(id)?.Amount ?? 0;
        Actor.Pokemon.RemoveShieldWithIDRPC(id);
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.Min(maximum, remaining + amount), id, 0, duration, true));
    }

    public override void OnNetworkDespawn()
    {
        StopAllCoroutines();
        EndPresentation();
        if (IsServer && Actor != null) Actor.Pokemon.OnDeath -= OnActorDeath;
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        EndPresentation();
        if (holdsAsset && assetHandle.IsValid()) Addressables.Release(assetHandle);
        holdsAsset = false;
        base.OnDestroy();
    }
}
