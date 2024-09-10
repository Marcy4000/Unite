using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SandstormArea : NetworkBehaviour
{
    private bool orangeTeam;
    private StatusEffect blind = new StatusEffect(StatusType.VisionObscuring, 3f, true, 0);

    private DamageInfo damageInfo;

    private bool initialized = false;
    private float stormDuration = 5f;
    private float stormTick = 0.6f;

    private List<Pokemon> pokemonInZone = new List<Pokemon>();
    private List<Pokemon> pokemonBlinded = new List<Pokemon>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 position, DamageInfo info, bool orangeTeam)
    {
        transform.position = position;
        damageInfo = info;
        this.orangeTeam = orangeTeam;

        pokemonBlinded.Clear();

        initialized = true;
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        stormDuration -= Time.deltaTime;
        stormTick -= Time.deltaTime;

        if (stormTick <= 0)
        {
            foreach (Pokemon pokemon in pokemonInZone)
            {
                if (pokemon == null)
                {
                    continue;
                }

                if (!Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, orangeTeam))
                {
                    continue;
                }

                pokemon.TakeDamage(damageInfo);

                if (pokemonBlinded.Contains(pokemon))
                {
                    continue;
                }

                pokemon.AddStatusEffect(blind);
                pokemonBlinded.Add(pokemon);
            }

            stormTick = 0.6f;
        }

        if (stormDuration <= 0)
        {
            NetworkObject.Despawn(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (!pokemonInZone.Contains(pokemon))
            {
                pokemonInZone.Add(pokemon);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (pokemonInZone.Contains(pokemon))
            {
                pokemonInZone.Remove(pokemon);
            }
        }
    }
}
