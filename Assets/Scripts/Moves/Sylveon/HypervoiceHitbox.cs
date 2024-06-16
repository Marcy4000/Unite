using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class HypervoiceHitbox : NetworkBehaviour
{
    private int wavesAmount = 6;

    private DamageInfo closeDamage;
    private DamageInfo farDamage;

    private bool teamToIgnore;
    public bool TeamToIgnore { get => teamToIgnore; set => SetTeamToIgnoreRPC(value); }

    private List<Pokemon> pokemonInTrigger = new List<Pokemon>();

    private float cooldown = 0.1f;

    [Rpc(SendTo.ClientsAndHost)]
    public void SetDamageRPC(DamageInfo close, DamageInfo far)
    {
        closeDamage = close;
        farDamage = far;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetTeamToIgnoreRPC(bool team)
    {
        teamToIgnore = team;
    }

    [Rpc(SendTo.Server)]
    public void DespawnRPC()
    {
        NetworkObject.Despawn(true);
    }

    void LateUpdate()
    {
        if (!IsServer)
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

                DamageInfo damageToUse = Vector3.Distance(pokemonInTrigger[i - 1].transform.position, transform.position) > 3.5f ? farDamage : closeDamage;
                pokemonInTrigger[i - 1].TakeDamage(damageToUse);
            }

            cooldown = 2.5f / wavesAmount;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        PlayerManager player;
        if (other.TryGetComponent(out player))
        {
            if (player.OrangeTeam == teamToIgnore)
            {
                return;
            }
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
