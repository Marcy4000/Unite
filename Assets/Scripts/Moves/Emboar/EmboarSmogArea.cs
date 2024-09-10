using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EmboarSmogArea : NetworkBehaviour
{
    private StatusEffect blindEffect = new StatusEffect(StatusType.VisionObscuring, 1.2f, true, 0);

    private List<Pokemon> hitPokemons = new List<Pokemon>();

    private float cooldown;
    private bool orangeTeam;
    private bool initialized;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 position, float duration, bool orangeTeam)
    {
        transform.position = position;
        cooldown = duration;
        this.orangeTeam = orangeTeam;
        initialized = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (!hitPokemons.Contains(pokemon) && Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, orangeTeam))
            {
                hitPokemons.Add(pokemon);
                pokemon.AddStatusEffect(blindEffect);
            }
        }
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        cooldown -= Time.deltaTime;

        if (cooldown <= 0)
        {
            NetworkObject.Despawn(true);
        }
    }
}
