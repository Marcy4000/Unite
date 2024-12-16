using Unity.Netcode;
using UnityEngine;

public class PsyduckRaceBarrierWall : NetworkBehaviour
{
    [SerializeField] private BoxCollider boxCollider;
    private float activeTime = 5f;

    private ulong ignoredObjectID;

    [Rpc(SendTo.Everyone)]
    public void InitializeRPC(Vector3 pos, Vector3 rot, ulong ignoredObjectID)
    {
        if (IsOwner)
        {
            transform.position = pos;
            transform.eulerAngles = rot;
            boxCollider.enabled = false;
        }
        this.ignoredObjectID = ignoredObjectID;
    }

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        activeTime -= Time.deltaTime;
        if (activeTime <= 0)
        {
            NetworkObject.Despawn(true);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer)
        {
            return;
        }

        if (collision.gameObject.CompareTag("Player"))
        {
            if (collision.gameObject.TryGetComponent(out PlayerManager playerManager))
            {
                if (playerManager.NetworkObjectId == ignoredObjectID)
                {
                    return;
                }
                playerManager.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.5f, true, 0));

                NetworkObject.Despawn(true);
            }     
        }
    }
}
