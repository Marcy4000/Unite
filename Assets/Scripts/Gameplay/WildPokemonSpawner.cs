using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class WildPokemonSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject pokemon;
    [SerializeField] private int expYield = 250;
    [SerializeField] private short energyYield = 5;
    [SerializeField] private float firstSpawnTime = 600f;
    [SerializeField] private float cooldown;
    [SerializeField] private bool canRespawn;
    [SerializeField] private bool isObjective;
    [SerializeField] private bool isSpawnedOnMap;

    private float timer;
    private bool spawnedFirstTime = false;

    public GameObject Pokemon => pokemon;
    public int ExpYield => expYield;
    public int EnergyYield => energyYield;
    public float Cooldown => cooldown;
    public bool CanRespawn => canRespawn;
    public bool IsObjective => isObjective;
    public bool IsSpawnedOnMap => isSpawnedOnMap;

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

        if (!canRespawn || isSpawnedOnMap || !spawnedFirstTime)
        {
            return;
        }

        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            SpawnPokemon();
        }
    }

    public void SpawnPokemon()
    {
        if (isSpawnedOnMap)
        {
            return;
        }

        WildPokemon wildPokemon = Instantiate(pokemon, transform.position, Quaternion.identity, transform).GetComponent<WildPokemon>();
        var instanceNetworkObject = wildPokemon.GetComponent<NetworkObject>();
        instanceNetworkObject.Spawn();
        wildPokemon.EnergyYield = energyYield;
        wildPokemon.ExpYield = expYield;
        wildPokemon.Pokemon.OnDeath += HandlePokemonDeath;
        isSpawnedOnMap = true;
    }

    private void HandlePokemonDeath(DamageInfo info)
    {
        isSpawnedOnMap = false;
        timer = cooldown;
    }
}