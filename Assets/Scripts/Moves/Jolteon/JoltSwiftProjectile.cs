using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class JoltSwiftProjectile : NetworkBehaviour
{
    private const string AssetPath = "Assets/Prefabs/Objects/Moves/Jolteon/JoltSwift.prefab";
    [SerializeField] private GameObject starImage;
    [SerializeField] private float maxDistance = 20f;
    private readonly NetworkVariable<bool> bigPresentation = new NetworkVariable<bool>(true);
    private PlayerManager actor;
    private GameObject projectilePrefab;
    private Vector3 direction;
    private ulong lastHit;
    private float traveledDistance;
    private bool initialized, isBigStar;
    private AsyncOperationHandle<GameObject> asset;

    public override void OnNetworkSpawn()
    {
        bigPresentation.OnValueChanged += OnSizeChanged;
        ApplySize(bigPresentation.Value);
        if (!IsServer) return;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
    }

    public void InitializeServer(PlayerManager owner, Vector3 travelDirection, bool big, GameObject prefab, ulong ignoredTarget = 0)
    {
        if (!IsServer || !IsSpawned || initialized || owner == null || prefab == null) return;
        actor = owner;
        projectilePrefab = prefab;
        Vector3 flat = new Vector3(travelDirection.x, 0f, travelDirection.z);
        direction = flat.sqrMagnitude > 0.001f ? flat.normalized : owner.transform.forward;
        isBigStar = big;
        lastHit = ignoredTarget;
        if (!big) maxDistance /= 1.5f;
        asset = Addressables.LoadAssetAsync<GameObject>(AssetPath);
        bigPresentation.Value = big;
        ApplySize(big);
        actor.Pokemon.OnDeath += OnActorDeath;
        initialized = true;
    }

    private void OnSizeChanged(bool previous, bool current) => ApplySize(current);
    private void ApplySize(bool big)
    {
        if (starImage != null) starImage.transform.localScale = big ? Vector3.one : Vector3.one * 0.5f;
    }

    private void Update()
    {
        if (!IsServer || !initialized) return;
        if (actor == null || !actor.IsSpawned || actor.Pokemon.CurrentHp <= 0) { Despawn(); return; }
        Vector3 movement = direction * (10f * Time.deltaTime);
        transform.position += movement;
        traveledDistance += movement.magnitude;
        if (traveledDistance < maxDistance) return;
        if (isBigStar) Split(0);
        Despawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !initialized || other == null) return;
        if (other.CompareTag("Wall"))
        {
            if (Physics.Raycast(new Ray(transform.position, direction), out RaycastHit hit, 10f * Time.deltaTime * 10f, LayerMask.GetMask("Default")))
            {
                Vector3 reflected = Vector3.Reflect(direction, hit.normal);
                reflected.y = 0f;
                if (reflected.sqrMagnitude > 0.001f) direction = reflected.normalized;
            }
            return;
        }
        if (!other.TryGetComponent(out Pokemon target) || target.NetworkObjectId == lastHit
            || !JoltMoveRuntime.CanHit(actor, target) || target.HasStatusEffect(StatusType.Invisible)) return;
        DamageInfo damage = isBigStar
            ? new DamageInfo(actor.NetworkObjectId, 1.2f, 8, 220, DamageType.Physical)
            : new DamageInfo(actor.NetworkObjectId, 1f, 6, 180, DamageType.Physical);
        target.TakeDamageRPC(damage);
        if (isBigStar) Split(target.NetworkObjectId);
        Despawn();
    }

    private void Split(ulong ignoredTarget)
    {
        if (!IsServer || !initialized || actor == null || projectilePrefab == null) return;
        initialized = false;
        SpawnChild(direction, ignoredTarget);
        SpawnChild(Quaternion.Euler(0f, 60f, 0f) * direction, ignoredTarget);
        SpawnChild(Quaternion.Euler(0f, -60f, 0f) * direction, ignoredTarget);
    }

    private void SpawnChild(Vector3 childDirection, ulong ignoredTarget)
    {
        GameObject instance = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        if (!instance.TryGetComponent(out NetworkObject network) || !instance.TryGetComponent(out JoltSwiftProjectile child))
        {
            Destroy(instance);
            return;
        }
        network.Spawn(true);
        child.InitializeServer(actor, childDirection, false, projectilePrefab, ignoredTarget);
        (actor.MovesController.Combat?.Character as JolteonCharacterRuntime)?.TrackSwiftProjectile(child);
    }

    private void OnActorDeath(DamageInfo damage) => Despawn();
    private void Despawn() { if (IsServer && IsSpawned) NetworkObject.Despawn(true); }

    public override void OnNetworkDespawn()
    {
        bigPresentation.OnValueChanged -= OnSizeChanged;
        if (actor != null && actor.Pokemon != null) actor.Pokemon.OnDeath -= OnActorDeath;
        initialized = false;
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        if (asset.IsValid()) Addressables.Release(asset);
        base.OnDestroy();
    }
}
