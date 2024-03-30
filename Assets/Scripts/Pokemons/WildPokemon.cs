using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WildPokemon : MonoBehaviour
{
    private Pokemon pokemon;
    [SerializeField] private int expYield = 250;
    [SerializeField] private int energyYield = 5;

    public Pokemon Pokemon => pokemon;
    public int ExpYield { get => expYield; set => expYield = value; }
    public int EnergyYield { get => energyYield; set => energyYield = value; }

    private void Awake()
    {
        pokemon = GetComponent<Pokemon>();
        pokemon.OnDeath += Die;
        pokemon.Type = PokemonType.Wild;
    }

    private void Die(DamageInfo info)
    {
        info.attacker.GainExperience(expYield);
        info.attacker.GetComponent<PlayerManager>().GainEnergy(energyYield);
        if (info.attacker.GetComponent<PlayerManager>())
        {
            info.attacker.GetComponent<PlayerManager>().MovesController.IncrementUniteCharge(5000);
        }
        Destroy(gameObject);
    }
}
