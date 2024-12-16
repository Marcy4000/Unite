using Unity.Netcode;
using UnityEngine;

public class PsyduckRaceHoamingProjectile : NetworkBehaviour
{
    [SerializeField] private bool isFreeze;

    public float speed = 5f;
    public float rotationSpeed = 5f;

    private Transform target;
    private GameObject pokemonToIgnore;

    private StatusType[] statuses = new StatusType[] { StatusType.Scriptable, StatusType.Incapacitated };

    [Rpc(SendTo.Server)]
    public void SetTargetRPC(ulong newTarget, ulong pokemonToIgnore, Vector3 startingPos)
    {
        transform.position = startingPos;

        try
        {
            target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[newTarget].transform;
            this.pokemonToIgnore = NetworkManager.Singleton.SpawnManager.SpawnedObjects[pokemonToIgnore].gameObject;
        }
        catch (System.Exception)
        {
            NetworkObject.Despawn(true);
        }
    }

    void Update()
    {
        if (!IsServer || target == null)
        {
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

        if (other.CompareTag("Player") && other.gameObject != pokemonToIgnore)
        {
            // Deal damage to the target
            PlayerManager playerController = other.GetComponent<PlayerManager>();
            if (playerController.Pokemon.HasAnyStatusEffect(statuses))
            {
                return;
            }

            if (isFreeze)
            {
                playerController.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Scriptable, 1f, true, 20));
            }
            else
            {
                playerController.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 1f, true, 0));
            }

            // Destroy the projectile
            NetworkObject.Despawn(true);
        }
    }
}
