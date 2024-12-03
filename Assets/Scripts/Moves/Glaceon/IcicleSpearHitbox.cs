using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class IcicleSpearHitbox : NetworkBehaviour
{
    private DamageInfo damageInfo;
    private StatChange enemySlow = new StatChange(20, Stat.Speed, 0.75f, true, false, true, 0);
    private Team teamToIgnore;
    private bool isUpgraded;

    private bool initialized = false;

    public DamageInfo DamageInfo => damageInfo;
    public Team TeamToIgnore => teamToIgnore;
    public bool IsUpgraded => isUpgraded;

    private List<Pokemon> pokemonInTrigger = new List<Pokemon>();

    private float cooldown = 0.75f;

    [Rpc(SendTo.Everyone)]
    public void InitializeRPC(DamageInfo damage, Team team, bool upgraded)
    {
        damageInfo = damage;
        teamToIgnore = team;
        isUpgraded = upgraded;
        initialized = true;
    }

    void Update()
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
                if (pokemonInTrigger[i-1] == null)
                {
                    pokemonInTrigger.RemoveAt(i-1);
                    continue;
                }

                pokemonInTrigger[i-1].TakeDamageRPC(damageInfo);
                pokemonInTrigger[i-1].AddStatChange(enemySlow);

                if (isUpgraded)
                {
                    short damage = (short)Mathf.FloorToInt(pokemonInTrigger[i - 1].GetMissingHp() * 0.05f);

                    if (pokemonInTrigger[i-1].Type == PokemonType.Wild)
                    {
                        damage = (short)Mathf.Clamp(damage, 0, 300);
                    }

                    pokemonInTrigger[i-1].TakeDamageRPC(new DamageInfo(damageInfo.attackerId, 0f, 0, damage, DamageType.Special));
                }
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

        if (!Aim.Instance.CanPokemonBeTargeted(other.gameObject, AimTarget.NonAlly, teamToIgnore))
        {
            return;
        }

        if (other.TryGetComponent(out Pokemon pokemon))
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

        if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (pokemonInTrigger.Contains(pokemon))
            {
                pokemonInTrigger.Remove(pokemon);
            }
        }
    }
}
