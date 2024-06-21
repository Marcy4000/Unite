using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class ElectroWebMark : NetworkBehaviour
{
    private Pokemon target;

    private DamageInfo tickDamage;

    private float tickRate = 0.6f;
    private float tickTimer = 0f;

    private float duration = 4f;

    private bool initialized = false;

    [Rpc(SendTo.Server)]
    public void SetTargetServerRPC(ulong targetId, DamageInfo tickDamage)
    {
        this.tickDamage = tickDamage;
        target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetId].GetComponent<Pokemon>();
        target.AddStatusEffect(new StatusEffect(StatusType.Scriptable, 0f, false, 8));
        initialized = true;
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        if (target == null)
        {
            NetworkObject.Despawn(true);
            return;
        }

        transform.position = target.transform.position;

        tickTimer += Time.deltaTime;

        if (tickTimer >= tickRate)
        {
            target.TakeDamage(tickDamage);
            tickTimer = 0f;
        }

        duration -= Time.deltaTime;

        if (duration <= 0)
        {
            target.RemoveStatusEffectWithID(8);
            NetworkObject.Despawn(true);
        }
    }
}
