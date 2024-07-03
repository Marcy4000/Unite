using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class WildPokemonSpawner : NetworkBehaviour
{
    public enum RespawnType
    {
        NoRespawn,
        TimedRespawn,
        SpecificTimesRespawn
    }

    [SerializeField] private GameObject pokemon;
    [SerializeField] private short wildPokemonID;
    [SerializeField] private float firstSpawnTime = 600f;
    [SerializeField] private float respawnCooldown;
    [SerializeField] private RespawnType respawnType;
    [SerializeField] private List<float> specificRespawnTimes;
    [SerializeField] private bool isObjective;
    [SerializeField] private bool isSpawnedOnMap;

    private float timer;
    private bool spawnedFirstTime = false;

    public GameObject Pokemon => pokemon;
    public float RespawnCooldown => respawnCooldown;
    public RespawnType PokemonRespawnType => respawnType;

    private void Update()
    {
        if (!IsServer)
        {
            return;
        }

        if (GameManager.Instance.GameTime.Value <= firstSpawnTime && firstSpawnTime != Mathf.NegativeInfinity)
        {
            SpawnPokemon();
            spawnedFirstTime = true;
            firstSpawnTime = Mathf.NegativeInfinity;
        }

        if (!spawnedFirstTime || IsPokemonSpawned())
        {
            return;
        }

        switch (respawnType)
        {
            case RespawnType.NoRespawn:
                // No respawn logic needed
                break;

            case RespawnType.TimedRespawn:
                HandleTimedRespawn();
                break;

            case RespawnType.SpecificTimesRespawn:
                HandleSpecificTimesRespawn();
                break;
        }
    }

    private void HandleTimedRespawn()
    {
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            SpawnPokemon();
        }
    }

    private void HandleSpecificTimesRespawn()
    {
        float currentTime = GameManager.Instance.GameTime.Value;
        foreach (float respawnTime in specificRespawnTimes)
        {
            if (Mathf.Approximately(currentTime, respawnTime))
            {
                SpawnPokemon();
                break;
            }
        }
    }

    public void SpawnPokemon()
    {
        if (IsPokemonSpawned())
        {
            return;
        }

        WildPokemon wildPokemon = Instantiate(pokemon, transform.position, transform.rotation, transform).GetComponent<WildPokemon>();
        var instanceNetworkObject = wildPokemon.GetComponent<NetworkObject>();
        instanceNetworkObject.Spawn();
        wildPokemon.SetWildPokemonInfoRPC(wildPokemonID, isObjective);
        wildPokemon.Pokemon.OnDeath += HandlePokemonDeath;
        isSpawnedOnMap = true;
    }

    private void HandlePokemonDeath(DamageInfo info)
    {
        isSpawnedOnMap = false;
        if (respawnType == RespawnType.TimedRespawn)
        {
            timer = respawnCooldown;
        }
    }

    private bool IsPokemonSpawned()
    {
        return isSpawnedOnMap;
    }
}
