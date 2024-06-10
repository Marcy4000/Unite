using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class IcicleSpearHitbox : NetworkBehaviour
{
    private DamageInfo damageInfo;
    private bool teamToIgnore;

    public DamageInfo DamageInfo { get => damageInfo; set => SetDamageRPC(value); }
    public bool TeamToIgnore { get => teamToIgnore; set => SetTeamToIgnoreRPC(value); }

    private List<Pokemon> pokemonInTrigger = new List<Pokemon>();

    private float cooldown = 0.75f;

    [Rpc(SendTo.ClientsAndHost)]
    private void SetDamageRPC(DamageInfo damage)
    {
        damageInfo = damage;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SetTeamToIgnoreRPC(bool team)
    {
        teamToIgnore = team;
    }

    void Update()
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
                if (pokemonInTrigger[i-1] == null)
                {
                    pokemonInTrigger.RemoveAt(i-1);
                    continue;
                }

                pokemonInTrigger[i-1].TakeDamage(damageInfo);
            }

            cooldown = 0.75f;
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
