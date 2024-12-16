using Unity.Netcode;
using UnityEngine;

public class HomingProjectile : NetworkBehaviour
{
    public float speed = 5f;
    public float rotationSpeed = 5f;

    private Transform target;
    private DamageInfo damageInfo;

    [Rpc(SendTo.Server)]
    public void SetTargetRPC(ulong newTarget, DamageInfo info)
    {
        target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[newTarget].transform;
        damageInfo = info;
    }

    void Update()
    {
        if (!IsServer)
        {
            return;
        }

        if (target == null)
        {
            NetworkObject.Despawn(true);
            return;
        }

        // Move towards the target
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        // Rotate towards the target
        Vector3 direction = (target.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.gameObject == target.gameObject)
        {
            // Deal damage to the target
            target.GetComponent<Pokemon>().TakeDamageRPC(damageInfo);

            // Destroy the projectile
            NetworkObject.Despawn(true);
        }
    }
}
