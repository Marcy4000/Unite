using Unity.Netcode;
using UnityEngine;

public class HomingProjectile : NetworkBehaviour
{
    public float speed = 5f;
    public float rotationSpeed = 5f;

    private Transform target;
    private DamageInfo damageInfo;

    public void SetTarget(Transform newTarget, DamageInfo info)
    {
        target = newTarget;
        damageInfo = info;
    }

    void Update()
    {
        if (target == null)
        {
            Destroy(gameObject); // Destroy the projectile if the target is lost
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
        if (other.gameObject == target.gameObject)
        {
            // Deal damage to the target
            target.GetComponent<Pokemon>().TakeDamage(damageInfo);

            // Destroy the projectile
            Destroy(gameObject);
        }
    }
}
