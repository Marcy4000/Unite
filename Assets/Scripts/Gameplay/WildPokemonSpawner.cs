using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WildPokemonSpawner : MonoBehaviour
{
    [SerializeField] private GameObject pokemon;
    [SerializeField] private int expYield = 250;
    [SerializeField] private int energyYield = 5;
    [SerializeField] private float firstSpawnTime = 600f;
    [SerializeField] private float cooldown;
    [SerializeField] private bool canRespawn;
    [SerializeField] private bool isObjective;
    [SerializeField] private bool isSpawned;

    private float timer;
    private bool spawnedFirstTime = false;

    public GameObject Pokemon => pokemon;
    public int ExpYield => expYield;
    public int EnergyYield => energyYield;
    public float Cooldown => cooldown;
    public bool CanRespawn => canRespawn;
    public bool IsObjective => isObjective;
    public bool IsSpawned => isSpawned;

    private void Update()
    {
        if (GameManager.instance.GameTime <= firstSpawnTime && firstSpawnTime != Mathf.NegativeInfinity)
        {
            SpawnPokemon();
            spawnedFirstTime = true;
            firstSpawnTime = Mathf.NegativeInfinity;
        }

        if (!canRespawn || isSpawned || !spawnedFirstTime)
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
        if (isSpawned)
        {
            return;
        }

        WildPokemon wildPokemon = Instantiate(pokemon, transform.position, Quaternion.identity, transform).GetComponent<WildPokemon>();
        wildPokemon.EnergyYield = energyYield;
        wildPokemon.ExpYield = expYield;
        wildPokemon.Pokemon.OnDeath += HandlePokemonDeath;
        isSpawned = true;
    }

    private void HandlePokemonDeath(DamageInfo info)
    {
        isSpawned = false;
        timer = cooldown;
    }
}