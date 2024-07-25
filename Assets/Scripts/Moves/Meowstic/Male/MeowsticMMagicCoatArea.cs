using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class MeowsticMMagicCoatArea : NetworkBehaviour
{
    [SerializeField] private LayerMask pokemonMask;
    private PlayerManager meowstic;

    private bool initialized = false;

    private float activeTime = 4f;

    private List<PlayerManager> playersInArea = new List<PlayerManager>();
    private Dictionary<PlayerManager, Action<NetworkListEvent<StatChange>>> statChangeEvents = new Dictionary<PlayerManager, Action<NetworkListEvent<StatChange>>>();

    [Rpc(SendTo.Server)]
    public void InitializeRPC(ulong meowsticID)
    {
        meowstic = NetworkManager.Singleton.SpawnManager.SpawnedObjects[meowsticID].GetComponent<PlayerManager>();
        playersInArea.Add(meowstic);
        statChangeEvents.Add(meowstic, (changeEvent) => OnPokemonStatChange(changeEvent, meowstic));
        meowstic.Pokemon.OnStatChange += statChangeEvents[meowstic];
        meowstic.Pokemon.OnDeath += OnMeowsticDeath;
        initialized = true;
    }

    private void OnMeowsticDeath(DamageInfo info)
    {
        meowstic.Pokemon.OnDeath -= OnMeowsticDeath;
        for (int i = playersInArea.Count - 1; i >= 0; i--)
        {
            PlayerManager player = playersInArea[i];
            player.Pokemon.OnStatChange -= statChangeEvents[player];
            statChangeEvents.Remove(player);
            playersInArea.RemoveAt(i);
        }
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
            meowstic.Pokemon.OnDeath -= OnMeowsticDeath;
            for (int i = playersInArea.Count - 1; i >= 0; i--)
            {
                PlayerManager player = playersInArea[i];
                player.Pokemon.OnStatChange -= statChangeEvents[player];
                statChangeEvents.Remove(player);
                playersInArea.RemoveAt(i);
            }
            NetworkObject.Despawn(true);
        }
    }

    private void OnPokemonStatChange(NetworkListEvent<StatChange> changeEvent, PlayerManager player)
    {
        if (!initialized || !IsServer)
        {
            return;
        }

        if (player.OrangeTeam != meowstic.OrangeTeam)
        {
            return;
        }

        if (changeEvent.Type == NetworkListEvent<StatChange>.EventType.Add && !changeEvent.Value.IsBuff && changeEvent.Value.IsTimed)
        {
            StartCoroutine(RemoveStatDelayed(changeEvent.Value, player));
        }
    }

    private IEnumerator RemoveStatDelayed(StatChange statChange, PlayerManager player)
    {
        yield return null;
        player.Pokemon.RemoveStatChangeRPC(statChange);

        Collider[] nearbyPokemons = Physics.OverlapSphere(transform.position, 8f, pokemonMask);
        List<PlayerManager> enemies = new List<PlayerManager>();

        foreach (var col in nearbyPokemons)
        {
            PlayerManager playerManager = col.GetComponent<PlayerManager>();
            if (playerManager != null && Aim.Instance.CanPokemonBeTargeted(playerManager.gameObject, AimTarget.Enemy, meowstic.OrangeTeam))
            {
                enemies.Add(playerManager);
            }
        }

        if (enemies.Count > 0)
        {
            enemies[UnityEngine.Random.Range(0, enemies.Count)].Pokemon.AddStatChange(statChange);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        PlayerManager player = other.GetComponent<PlayerManager>();

        if (player != null && player != meowstic && !playersInArea.Contains(player))
        {
            playersInArea.Add(player);
            statChangeEvents.Add(player, (changeEvent) => OnPokemonStatChange(changeEvent, player));
            player.Pokemon.OnStatChange += statChangeEvents[player];
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer)
        {
            return;
        }

        PlayerManager player = other.GetComponent<PlayerManager>();

        if (player != null && playersInArea.Contains(player))
        {
            playersInArea.Remove(player);
            player.Pokemon.OnStatChange -= statChangeEvents[player];
            statChangeEvents.Remove(player);
        }
    }
}
