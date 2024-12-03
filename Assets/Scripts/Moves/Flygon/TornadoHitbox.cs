using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TornadoHitbox : NetworkBehaviour
{
    [SerializeField] private LayerMask enemies;
    private StatusEffect tornadoStun = new StatusEffect(StatusType.Incapacitated, 1f, true, 6);

    private DamageInfo damageInfo;

    private Team orangeTeam;
    private bool initialized;

    private Collider[] colliders = new Collider[15];

    private List<Pokemon> stunnedPokemons = new List<Pokemon>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(DamageInfo damageInfo, Team isOrangeTeam)
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
            if (colliders[i].TryGetComponent(out Pokemon pokemon))
            {
                if (!Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, orangeTeam) || stunnedPokemons.Contains(pokemon))
                {
                    continue;
                }

                Vector3 direction = (transform.position - pokemon.transform.position).normalized;

                pokemon.ApplyKnockbackRPC(direction, 0.1f);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (stunnedPokemons.Contains(pokemon) || !Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, orangeTeam))
            {
                return;
            }

            pokemon.AddStatusEffect(tornadoStun);
            pokemon.ApplyKnockupRPC(2f, 0.6f);
            stunnedPokemons.Add(pokemon);
            pokemon.StartCoroutine(DamagePokemon(pokemon));
        }
    }

    private IEnumerator DamagePokemon(Pokemon pokemon)
    {
        yield return new WaitForSeconds(0.6f);

        pokemon.TakeDamageRPC(damageInfo);

        yield return new WaitForSeconds(0.4f);

        stunnedPokemons.Remove(pokemon);
    }
}
