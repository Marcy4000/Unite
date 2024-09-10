using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SupersonicHitbox : NetworkBehaviour
{
    private StatusEffect confusionEffect = new StatusEffect(StatusType.Incapacitated, 1.2f, true, 0);
    private StatChange speedReduction = new StatChange(20, Stat.Speed, 4.2f, true, false, true, 0);

    private bool orangeTeam;
    private bool initialized;

    private float cooldown = 0.4f;

    private List<Pokemon> pokemonInTrigger = new List<Pokemon>();
    private List<Pokemon> stunnedPokemons = new List<Pokemon>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 startPos, Vector3 startRot, bool orangeTeam)
    {
        transform.position = startPos;
        transform.rotation = Quaternion.LookRotation(startRot);
        this.orangeTeam = orangeTeam;

        stunnedPokemons.Clear();

        initialized = true;

        StartCoroutine(Despawn());
    }

    private IEnumerator Despawn()
    {
        yield return new WaitForSeconds(1.2f);
        NetworkObject.Despawn(true);
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        cooldown -= Time.deltaTime;

        if (cooldown <= 0f)
        {
            for (int i = pokemonInTrigger.Count; i > 0; i--)
            {
                if (pokemonInTrigger[i - 1] == null)
                {
                    pokemonInTrigger.RemoveAt(i - 1);
                    continue;
                }

                StunPokemon(pokemonInTrigger[i - 1]);
            }

            cooldown = 0.75f;
        }
    }

    private void StunPokemon(Pokemon pokemon)
    {
        if (stunnedPokemons.Contains(pokemon))
        {
            return;
        }

        if (!Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, orangeTeam))
        {
            return;
        }

        stunnedPokemons.Add(pokemon);
        pokemon.AddStatusEffect(confusionEffect);
        pokemon.AddStatChange(speedReduction);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        Pokemon pokemon;

        if (other.TryGetComponent(out pokemon))
        {
            if (!pokemonInTrigger.Contains(pokemon))
            {
                pokemonInTrigger.Add(pokemon);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        Pokemon pokemon;

        if (other.TryGetComponent(out pokemon))
        {
            if (pokemonInTrigger.Contains(pokemon))
            {
                pokemonInTrigger.Remove(pokemon);
            }
        }
    }
}
