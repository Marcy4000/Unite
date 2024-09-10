using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class GlaceUniteArea : NetworkBehaviour
{
    private DamageInfo damageInfo;
    private bool orangeTeam;
    private NetworkVariable<ulong> glaceonID = new NetworkVariable<ulong>();

    private NetworkVariable<bool> zoneActive = new NetworkVariable<bool>(false);

    private float activeTimer = 0f;
    private float glaceonSpearsTimer = 1f;

    private StatChange glaceonBuff = new StatChange(50, Stat.Speed, 6f, true, true, true, 0);
    private StatChange enemiesDebuff = new StatChange(50, Stat.Speed, 1f, true, false, true, 0);

    private List<Pokemon> pokemonInZone = new List<Pokemon>();

    public event System.Action onGiveGlaceonSpears;

    [Rpc(SendTo.Server)]
    public void InitializeRPC(Vector3 position, Quaternion rotation, DamageInfo info, bool orangeTeam, ulong glaceonID)
    {
        transform.position = position;
        transform.rotation = rotation;
        damageInfo = info;
        this.orangeTeam = orangeTeam;
        this.glaceonID.Value = glaceonID;
        activeTimer = 8.5f;
        StartCoroutine(ApplyBuff());
        StartCoroutine(ApplyDebuff());
        StartCoroutine(DamageEnemies());
        zoneActive.Value = true;
    }

    private void Update()
    {
        if (!zoneActive.Value)
        {
            return;
        }

        if (IsOwner)
        {
            glaceonSpearsTimer -= Time.deltaTime;

            if (glaceonSpearsTimer <= 0)
            {
                foreach (Pokemon pokemon in pokemonInZone)
                {
                    if (pokemon.NetworkObjectId == glaceonID.Value)
                    {
                        onGiveGlaceonSpears?.Invoke();
                        break;
                    }
                }
                glaceonSpearsTimer = 1f;
            }
        }

        if (!IsServer)
        {
            return;
        }

        activeTimer -= Time.deltaTime;

        if (activeTimer <= 0)
        {
            zoneActive.Value = false;
            NetworkObject.Despawn(true);
        }
    }

    private IEnumerator DamageEnemies()
    {
        yield return new WaitForSeconds(0.2f);

        foreach (Pokemon pokemon in pokemonInZone)
        {
            if (!zoneActive.Value)
            {
                yield return null;
            }

            if (Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, orangeTeam))
            {
                pokemon.TakeDamage(damageInfo);
            }
        }
    }

    private IEnumerator ApplyBuff()
    {
        while (true)
        {
            foreach (Pokemon pokemon in pokemonInZone)
            {
                if (pokemon.NetworkObjectId == glaceonID.Value)
                {
                    pokemon.AddStatChange(glaceonBuff);
                    break;
                }
            }

            yield return new WaitForSeconds(6f);
        }
    }

    private IEnumerator ApplyDebuff()
    {
        while (true)
        {
            if (!zoneActive.Value)
            {
                yield return null;
            }

            foreach (Pokemon pokemon in pokemonInZone)
            {
                if (pokemon.NetworkObjectId == glaceonID.Value)
                {
                    continue;
                }

                if (Aim.Instance.CanPokemonBeTargeted(pokemon.gameObject, AimTarget.NonAlly, orangeTeam))
                {
                    pokemon.AddStatChange(enemiesDebuff);
                }
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Pokemon pokemon = other.GetComponent<Pokemon>();
        if (pokemon != null && !pokemonInZone.Contains(pokemon))
        {
            pokemonInZone.Add(pokemon);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Pokemon pokemon = other.GetComponent<Pokemon>();
        if (pokemon != null && pokemonInZone.Contains(pokemon))
        {
            pokemonInZone.Remove(pokemon);
        }
    }
}
