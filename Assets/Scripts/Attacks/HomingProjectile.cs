using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class HomingProjectile : NetworkBehaviour
{
    public float speed = 5f;
    public float rotationSpeed = 5f;
    [SerializeField] private StatChange[] attackerOnHitStats;

    private Transform target;
    private DamageInfo damageInfo;
    private AsyncOperationHandle<GameObject> assetHandle;
    private bool holdsAsset;

    public void HoldAsset(AsyncOperationHandle<GameObject> handle) { assetHandle = handle; holdsAsset = true; }

    [Rpc(SendTo.Server)]
    public void SetTargetRPC(ulong newTarget, DamageInfo info)
    {
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(newTarget, out var recipient))
        {
            NetworkObject.Despawn(true);
            return;
        }
        target = recipient.transform;
        damageInfo = info;
    }

    void Update()
    {
        if (!IsServer)
        {
            return;
        }

        if (target == null || (target.TryGetComponent(out Pokemon recipient) && recipient.CurrentHp <= 0))
        {
            NetworkObject.Despawn(true);
            return;
        }

        // Move towards the target
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        // Rotate towards the target
        Vector3 direction = (target.position - transform.position).normalized;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        if (target == null)
        {
            return;
        }

        if (other.gameObject == target.gameObject)
        {
            // Deal damage to the target
            var recipient = target.GetComponent<Pokemon>();
            bool landed = recipient.CurrentHp > 0 && !recipient.HasStatusEffect(StatusType.Invincible)
                && !recipient.HasStatusEffect(StatusType.Untargetable);
            if (landed) recipient.TakeDamageRPC(damageInfo);
            if (landed && attackerOnHitStats != null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(damageInfo.attackerId, out var attackerObject)
                && attackerObject.TryGetComponent(out Pokemon attacker) && attacker.CurrentHp > 0)
                foreach (StatChange stat in attackerOnHitStats)
                {
                    attacker.RemoveAllStatChangeWithIDRPC(stat.ID);
                    attacker.AddStatChange(stat);
                }

            // Destroy the projectile
            NetworkObject.Despawn(true);
        }
    }

    public override void OnDestroy()
    {
        if (holdsAsset && assetHandle.IsValid()) Addressables.Release(assetHandle);
        holdsAsset = false;
        base.OnDestroy();
    }
}
