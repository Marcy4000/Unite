using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WildPokemon : MonoBehaviour
{
    private Pokemon pokemon;
    [SerializeField] private int expYield = 250;

    private void Start()
    {
        pokemon = GetComponent<Pokemon>();
        pokemon.OnDeath += Die;
        pokemon.Type = PokemonType.Wild;
    }

    private void Die(DamageInfo info)
    {
        info.attacker.GainExperience(expYield);
        Destroy(gameObject);
    }
}
