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

    [SerializeField] private GameObject pokemonPrefab;
    [SerializeField] private AvailableWildPokemons wildPokemonID;
    [SerializeField] private bool usesTimeRemaining = true;
    [SerializeField] private float firstSpawnTime = 600f;
    [SerializeField] private float respawnCooldown;
    [SerializeField] private RespawnType respawnType;
    [SerializeField] private List<float> specificRespawnTimes;
    [SerializeField] private float despawnTime;
    [SerializeField] private bool isObjective;
    [SerializeField] private bool isSpawnedOnMap;
    [SerializeField] private int soldierLaneID;

    private float timer;
    private bool spawnedFirstTime = false;

    private WildPokemon wildPokemon;

    public WildPokemon WildPokemon => wildPokemon;
    public float RespawnCooldown => respawnCooldown;
    public RespawnType PokemonRespawnType => respawnType;

    private void Start()
    {
        if (usesTimeRemaining)
        {
            if (firstSpawnTime > 0) 
            {
                firstSpawnTime = GameManager.Instance.MAX_GAME_TIME - firstSpawnTime;
            }
            else
            {
                firstSpawnTime = Mathf.Infinity;
            }

            if (despawnTime > 0f)
            {
                despawnTime = GameManager.Instance.MAX_GAME_TIME - despawnTime;
            }

            for (int i = 0; i < specificRespawnTimes.Count; i++)
            {
                specificRespawnTimes[i] = GameManager.Instance.MAX_GAME_TIME - specificRespawnTimes[i];
            }
        }
    }

    private void Update()
    {
        if (!IsServer || GameManager.Instance.GameState != GameState.Playing)
        {
            return;
        }

        if (despawnTime > 0f && GameManager.Instance.GameTime >= despawnTime)
        {
            DespawnPokemon(false);
            return;
        }

        if (GameManager.Instance.GameTime >= firstSpawnTime && firstSpawnTime != Mathf.Infinity)
        {
            SpawnPokemon();
            spawnedFirstTime = true;
            firstSpawnTime = Mathf.Infinity;
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
        float currentTime = GameManager.Instance.GameTime;
        foreach (float respawnTime in specificRespawnTimes)
        {
            if (Mathf.Approximately(currentTime, respawnTime))
            {
                SpawnPokemon();
                break;
            }
        }
    }

    public void EnableSpawner()
    {
        if (!IsServer)
        {
            return;
        }
        firstSpawnTime = GameManager.Instance.GameTime;
    }

    public void SpawnPokemon()
    {
        if (IsPokemonSpawned() || !IsServer)
        {
            return;
        }

        wildPokemon = Instantiate(pokemonPrefab, transform.position, transform.rotation, transform).GetComponent<WildPokemon>();
        var instanceNetworkObject = wildPokemon.GetComponent<NetworkObject>();
        instanceNetworkObject.Spawn();
        wildPokemon.SetWildPokemonInfoRPC((short)wildPokemonID, isObjective);
        wildPokemon.SoldierLaneID = soldierLaneID;
        wildPokemon.Pokemon.OnDeath += HandlePokemonDeath;
        isSpawnedOnMap = true;
    }

    public void DespawnPokemon(bool canRespawn)
    {
        if (!IsPokemonSpawned() || !IsServer)
        {
            return;
        }

        if (!canRespawn)
        {
            respawnType = RespawnType.NoRespawn;
        }
        wildPokemon.Pokemon.TakeDamage(new DamageInfo(wildPokemon.NetworkObjectId, 999f, 999, 9999, DamageType.True));
    }

    private void HandlePokemonDeath(DamageInfo info)
    {
        isSpawnedOnMap = false;
        wildPokemon = null;
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
