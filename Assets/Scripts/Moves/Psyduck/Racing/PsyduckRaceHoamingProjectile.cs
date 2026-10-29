using Unity.Netcode;
using UnityEngine;

public class PsyduckRaceHoamingProjectile : NetworkBehaviour
{
    [SerializeField] private bool isFreeze;

    public float speed = 5f;
    public float rotationSpeed = 5f;

    private Transform target;
    private NetworkObject targetNetworkObject;
    private GameObject pokemonToIgnore;

    private float timer = 10f;
    private bool initialized;

    private void Awake() { PsyduckRaceHit.PrepareTrigger(gameObject); }

    [Rpc(SendTo.Server)]
    public void SetTargetRPC(ulong newTarget, ulong pokemonToIgnore, Vector3 startingPos)
    {
        if (initialized) return;
        transform.position = startingPos;
        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(newTarget, out var targetObject)
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(pokemonToIgnore, out var owner))
        {
            NetworkObject.Despawn(true);
            return;
        }
        target = targetObject.transform;
        targetNetworkObject = targetObject;
        this.pokemonToIgnore = owner.gameObject;
        initialized = true;
    }

    void Update()
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0 || (initialized && (target == null || targetNetworkObject == null || !targetNetworkObject.IsSpawned)))
        {
            NetworkObject.Despawn(true);
            return;
        }
        if (!initialized) return;

        // Move towards the target
        transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

        // Rotate towards the target
        Vector3 direction = (target.position - transform.position).normalized;
        if (direction.sqrMagnitude < 0.0001f) return;
        Quaternion lookRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned || !initialized)
        {
            return;
        }

        var playerController = other.GetComponentInParent<PlayerManager>();
        if (playerController != null && playerController.gameObject != pokemonToIgnore)
        {
            var effect = isFreeze ? new StatusEffect(StatusType.Scriptable, 1f, true, 20)
                : new StatusEffect(StatusType.Incapacitated, 1f, true, 0);
            if (PsyduckRaceHit.Apply(playerController, effect)) NetworkObject.Despawn(true);
        }
    }
}
