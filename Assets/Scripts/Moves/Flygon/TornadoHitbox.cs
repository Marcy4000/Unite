using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TornadoHitbox : NetworkBehaviour
{
    [SerializeField] private LayerMask enemies;
    private StatusEffect tornadoStun = new StatusEffect(StatusType.Incapacitated, 1f, true, 6);

    private DamageInfo damageInfo;

    private bool orangeTeam;
    private bool initialized;

    private Collider[] colliders = new Collider[15];

    private List<Pokemon> stunnedPokemons = new List<Pokemon>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(DamageInfo damageInfo, bool isOrangeTeam)
    {
        this.damageInfo = damageInfo;
        orangeTeam = isOrangeTeam;

        initialized = true;
    }

    [Rpc(SendTo.Server)]
    public void UpdateDamageRPC(DamageInfo damageInfo)
    {
        this.damageInfo = damageInfo;
    }

    [Rpc(SendTo.Server)]
    public void DespawnRPC()
    {
        NetworkObject.Despawn(true);
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        int colliderCount = Physics.OverlapSphereNonAlloc(transform.position + new Vector3(0f, 0.3f, 0f), 4f, colliders, enemies);

        for (int i = 0; i < colliderCount; i++)
        {
            if (colliders[i].TryGetComponent(out PlayerManager player))
            {
                if (player.OrangeTeam == orangeTeam)
                {
                    continue;
                }

                Vector3 direction = (transform.position - player.transform.position).normalized;

                player.PlayerMovement.KnockbackRPC(direction, 0.1f);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        if (other.TryGetComponent(out PlayerManager player))
        {
            if (player.OrangeTeam == orangeTeam)
            {
                return;
            }
        }

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (stunnedPokemons.Contains(pokemon))
            {
                return;
            }

            pokemon.AddStatusEffect(tornadoStun);
            stunnedPokemons.Add(pokemon);
            pokemon.StartCoroutine(DamagePokemon(pokemon));
        }
    }

    private IEnumerator DamagePokemon(Pokemon pokemon)
    {
        yield return new WaitForSeconds(0.8f);

        pokemon.TakeDamage(damageInfo);
    }
}
