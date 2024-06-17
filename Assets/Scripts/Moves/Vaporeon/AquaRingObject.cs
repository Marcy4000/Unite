using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class AquaRingObject : NetworkBehaviour
{
    // TODO: After reworking shields, aqua ring should be canceled when the shield breaks

    private ulong targetId;
    private PlayerManager target;

    private DamageInfo healAmount;

    private float healCooldown = 0.7f;
    private float ringDuration = 6f;

    private bool initialized;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(ulong target, DamageInfo heal)
    {
        targetId = target;
        healAmount = heal;
        this.target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetId].GetComponent<PlayerManager>();
        this.target.Pokemon.AddShield(Mathf.FloorToInt(this.target.Pokemon.GetMaxHp() * 0.10f));
        initialized = true;
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        healCooldown -= Time.deltaTime;
        ringDuration -= Time.deltaTime;
        transform.position = target.transform.position;

        if (healCooldown <= 0)
        {
            target.Pokemon.HealDamage(healAmount);

            healCooldown = 0.7f;
        }

        if (ringDuration <= 0)
        {
            NetworkObject.Despawn(true);
        }
    }
}
