using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class WildPokemon : NetworkBehaviour
{
    private Pokemon pokemon;
    [SerializeField] private int expYield = 250;
    [SerializeField] private short energyYield = 5;
    [SerializeField] private PokemonBase pokemonBase;

    public Pokemon Pokemon => pokemon;
    public int ExpYield { get => expYield; set => expYield = value; }
    public short EnergyYield { get => energyYield; set => energyYield = value; }

    public override void OnNetworkSpawn()
    {
        pokemon = GetComponent<Pokemon>();
        pokemon.SetNewPokemon(pokemonBase);
        pokemon.OnDeath += Die;
        pokemon.Type = PokemonType.Wild;
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
        attacker.GetComponent<PlayerManager>().GainEnergy(energyYield);
        if (attacker.GetComponent<PlayerManager>())
        {
            attacker.GetComponent<PlayerManager>().MovesController.IncrementUniteCharge(5000);
        }
        if (IsServer)
        {
            gameObject.GetComponent<NetworkObject>().Despawn(true);
        }
        else
        {
            DespawnRPC();
        }
    }

    [Rpc(SendTo.Server)]
    private void DespawnRPC()
    {
        gameObject.GetComponent<NetworkObject>().Despawn(true);
    }
}
