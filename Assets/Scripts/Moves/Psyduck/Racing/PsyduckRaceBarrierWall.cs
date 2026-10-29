using Unity.Netcode;
using UnityEngine;

public class PsyduckRaceBarrierWall : NetworkBehaviour
{
    [SerializeField] private BoxCollider boxCollider;
    private float activeTime = 5f;

    private ulong ignoredObjectID;
    private bool initialized;

    private void Awake()
    {
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        PsyduckRaceHit.PrepareTrigger(gameObject);
    }

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 pos, Vector3 rot, ulong ignoredObjectID)
    {
        if (initialized) return;
        transform.position = pos;
        transform.eulerAngles = rot;
        boxCollider.enabled = true;
        this.ignoredObjectID = ignoredObjectID;
        initialized = true;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        activeTime -= Time.deltaTime;
        if (activeTime <= 0)
        {
            NetworkObject.Despawn(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !IsSpawned || !initialized)
        {
            return;
        }

        var playerManager = other.GetComponentInParent<PlayerManager>();
        if (playerManager != null && playerManager.NetworkObjectId != ignoredObjectID)
        {
            if (PsyduckRaceHit.Apply(playerManager, new StatusEffect(StatusType.Incapacitated, 0.5f, true, 0)))
                NetworkObject.Despawn(true);
        }
    }
}
