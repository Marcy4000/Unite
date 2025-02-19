using Unity.Netcode;
using UnityEngine;

public class LunarBlessingShield : NetworkBehaviour
{
    private Pokemon target;

    private bool initialized = false;

    private float timer = 0f;

    [Rpc(SendTo.Server)]
    public void SetTargetServerRPC(ulong targetId)
    {
        target = NetworkManager.Singleton.SpawnManager.SpawnedObjects[targetId].GetComponent<Pokemon>();
        target.OnDeath += (killer) => NetworkObject.Despawn(true);
        target.OnShieldChange += CheckForShield;
        timer = 4.8f;
        initialized = true;
    }

    private void CheckForShield()
    {
        if (!target.HasShieldWithID(8))
        {
            target.OnShieldChange -= CheckForShield;
            NetworkObject.Despawn(true);
        }
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

        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }
        else
        {
            target.OnShieldChange -= CheckForShield;
            if (target.HasShieldWithID(8))
            {
                target.HealDamageRPC(target.GetShieldWithID(8).Value.Amount);
            }
            NetworkObject.Despawn(true);
        }
    }
}
