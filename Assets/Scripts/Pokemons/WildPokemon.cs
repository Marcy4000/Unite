using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class WildPokemon : NetworkBehaviour
{
    private Pokemon pokemon;
    [SerializeField] private int expYield = 250;
    [SerializeField] private short energyYield = 5;
    [SerializeField] private PokemonBase pokemonBase;

    private string resourcePath = "Objects/AeosEnergy";

    public Pokemon Pokemon => pokemon;
    public int ExpYield { get => expYield; set => expYield = value; }
    public short EnergyYield { get => energyYield; set => energyYield = value; }

    public override void OnNetworkSpawn()
    {
        pokemon = GetComponent<Pokemon>();
        pokemon.SetNewPokemon(pokemonBase);
        pokemon.Type = PokemonType.Wild;
        NetworkObject.DestroyWithScene = true;
        if (IsServer)
        {
            pokemon.OnDeath += Die;
        }
    }

    private void Die(DamageInfo info)
    {
        GiveExpRpc(info.attackerId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void GiveExpRpc(ulong attackerID)
    {
        Pokemon attacker = NetworkManager.Singleton.SpawnManager.SpawnedObjects[attackerID].GetComponent<Pokemon>();

        attacker.GainExperience(expYield);
        GiveAttackerEnergy(attacker.GetComponent<PlayerManager>());
        if (attacker.GetComponent<PlayerManager>())
        {
            attacker.GetComponent<PlayerManager>().MovesController.IncrementUniteCharge(5000);
        }

        if (IsServer)
        {
            gameObject.GetComponent<NetworkObject>().Despawn(true);
        }
    }

    private void GiveAttackerEnergy(PlayerManager attacker)
    {
        if (attacker.AvailableEnergy() >= energyYield)
        {
            attacker.GainEnergy(energyYield);
        }
        else
        {
            SpawnEnergy((short)(energyYield - attacker.AvailableEnergy()));
            attacker.GainEnergy(attacker.AvailableEnergy());
        }
    }

    private void SpawnEnergy(short amount)
    {
        int numFives = amount / 5;
        int remainderOnes = amount % 5;

        for (int i = 0; i < numFives; i++)
        {
            SpawnEnergyRpc(true);
        }

        for (int i = 0; i < remainderOnes; i++)
        {
            SpawnEnergyRpc(false);
        }
    }

    [Rpc(SendTo.Server)]
    private void SpawnEnergyRpc(bool isBig)
    {
        Vector3 offset = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
        GameObject energy = Instantiate(Resources.Load(resourcePath, typeof(GameObject)), transform.position + offset, Quaternion.identity) as GameObject;
        energy.GetComponent<AeosEnergy>().LocalBigEnergy = isBig;
        energy.GetComponent<NetworkObject>().Spawn();
    }
}
