using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WildPokemon : MonoBehaviour
{
    private Pokemon pokemon;
    [SerializeField] private int expYield = 250;
    [SerializeField] private int energyYield = 5;

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
        Destroy(gameObject);
    }
}
