using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MeowsticMUniteArea : NetworkBehaviour
{
    private PlayerManager meowstic;

    private bool initialized = false;

    private float activeTime = 6f;

    private DamageInfo damage = new DamageInfo(0, 0.5f, 4, 75, DamageType.Special);

    private StatChange speedBoost = new StatChange(20, Stat.Speed, 0f, false, true, true, 17);
    private StatChange cdrBoost = new StatChange(10, Stat.Cdr, 0f, false, true, true, 18);

    private StatChange speedDebuff = new StatChange(25, Stat.Speed, 3f, false, false, true, 19);

    private List<Pokemon> enemiesInArea = new List<Pokemon>();
    private List<Pokemon> alliesInArea = new List<Pokemon>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(ulong meowsticID)
    {
        meowstic = NetworkManager.Singleton.SpawnManager.SpawnedObjects[meowsticID].GetComponent<PlayerManager>();
        damage.attackerId = meowsticID;
        meowstic.Pokemon.OnDeath += OnMeowsticDeath;

        initialized = true;

        StartCoroutine(DoDamage());
    }

    private void OnMeowsticDeath(DamageInfo info)
    {
        foreach (Pokemon enemy in enemiesInArea)
        {
            if (enemy != null)
            {
                enemy.RemoveStatChangeWithIDRPC(speedDebuff.ID);
            }
        }

        foreach (Pokemon ally in alliesInArea)
        {
            if (ally != null)
            {
                ally.RemoveStatChangeWithIDRPC(speedBoost.ID);
                ally.RemoveStatChangeWithIDRPC(cdrBoost.ID);
                if (ally.HasShieldWithID(8))
                {
                    ally.RemoveShieldWithIDRPC(8);
                }
            }
        }
        meowstic.Pokemon.OnDeath -= OnMeowsticDeath;
        NetworkObject.Despawn(true);
    }

    private void Update()
    {
        if (!IsServer || !initialized)
        {
            return;
        }

        transform.position = meowstic.transform.position;

        activeTime -= Time.deltaTime;

        if (activeTime <= 0)
        {
            foreach (Pokemon enemy in enemiesInArea)
            {
                if (enemy != null)
                {
                    enemy.RemoveStatChangeWithIDRPC(speedDebuff.ID);
                }
            }

            foreach (Pokemon ally in alliesInArea)
            {
                if (ally != null)
                {
                    ally.RemoveStatChangeWithIDRPC(speedBoost.ID);
                    ally.RemoveStatChangeWithIDRPC(cdrBoost.ID);
                    if (ally.HasShieldWithID(8))
                    {
                        ally.RemoveShieldWithIDRPC(8);
                    }
                }
            }
            meowstic.Pokemon.OnDeath -= OnMeowsticDeath;
            NetworkObject.Despawn(true);
        }
    }

    private IEnumerator DoDamage()
    {
        while (activeTime > 0)
        {
            foreach (Pokemon enemy in enemiesInArea)
            {
                if (enemy != null && Aim.Instance.CanPokemonBeTargeted(enemy.gameObject, AimTarget.NonAlly, meowstic.CurrentTeam))
                {
                    enemy.TakeDamage(damage);
                }
            }
            yield return new WaitForSeconds(1f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        if (other.TryGetComponent(out PlayerManager player))
        {


            if (player.CurrentTeam.IsOnSameTeam(meowstic.CurrentTeam))
            {
                if (!alliesInArea.Contains(player.Pokemon))
                {
                    alliesInArea.Add(player.Pokemon);
                    player.Pokemon.AddStatChange(speedBoost);
                    player.Pokemon.AddStatChange(cdrBoost);
                    if (!player.Pokemon.HasShieldWithID(8))
                    {
                        player.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.RoundToInt(player.Pokemon.GetMaxHp() * 0.25f), 8, 0, 0, false));
                    }
                }
            }
            else
            {
                if (!enemiesInArea.Contains(player.Pokemon))
                {
                    enemiesInArea.Add(player.Pokemon);
                    player.Pokemon.AddStatChange(speedDebuff);
                }
            }
        }
        else if (other.TryGetComponent(out Pokemon pokemon))
        {
            if (!enemiesInArea.Contains(pokemon))
            {
                enemiesInArea.Add(pokemon);
                pokemon.AddStatChange(speedDebuff);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        Pokemon pokemon = other.GetComponent<Pokemon>();

        if (pokemon != null && enemiesInArea.Contains(pokemon))
        {
            pokemon.RemoveStatChangeWithIDRPC(speedDebuff.ID);
            if (pokemon.HasShieldWithID(8))
            {
                pokemon.RemoveShieldWithIDRPC(8);
            }
            enemiesInArea.Remove(pokemon);
        }
        else if (pokemon != null && alliesInArea.Contains(pokemon))
        {
            pokemon.RemoveStatChangeWithIDRPC(speedBoost.ID);
            pokemon.RemoveStatChangeWithIDRPC(cdrBoost.ID);
            alliesInArea.Remove(pokemon);
        }
    }
}
