using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class DischargeHitbox : NetworkBehaviour
{
    private PlayerManager jolteon;

    private DamageInfo damageInfo;

    private bool initialized = false;

    private List<Pokemon> pokemonInTrigger = new List<Pokemon>();

    private float damageTimer = 0.45f;

    [Rpc(SendTo.Owner)]
    public void InitializeDischargeHitboxRpc(Vector3 position, DamageInfo damageInfo, ulong playerID)
    {
        this.damageInfo = damageInfo;
        transform.position = position;
        jolteon = NetworkManager.Singleton.SpawnManager.SpawnedObjects[playerID].gameObject.GetComponent<PlayerManager>();
        initialized = true;
    }

    [Rpc(SendTo.Server)]
    public void DespawnRPC()
    {
        NetworkObject.Despawn(true);
    }

    private void Update()
    {
        if (!initialized || !IsOwner)
        {
            return;
        }
        
        transform.position = jolteon.transform.position + new Vector3(0f, 1f, 0f);

        damageTimer -= Time.deltaTime;

        if (damageTimer <= 0)
        {
            foreach (Pokemon pokemon in pokemonInTrigger)
            {
                if (pokemon != null)
                {
                    pokemon.TakeDamage(damageInfo);
                }
            }

            damageTimer = 0.45f;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOwner)
        {
            return;
        }

        if (other.TryGetComponent(out PlayerManager player))
        {
            if (player.OrangeTeam == jolteon.OrangeTeam)
            {
                return;
            }

            if (!pokemonInTrigger.Contains(player.Pokemon))
            {
                pokemonInTrigger.Add(player.Pokemon);
            }
        }
        else if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (!pokemonInTrigger.Contains(pokemon))
            {
                pokemonInTrigger.Add(pokemon);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsOwner)
        {
            return;
        }

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (pokemonInTrigger.Contains(pokemon))
            {
                pokemonInTrigger.Remove(pokemon);
            }
        }
    }
}
