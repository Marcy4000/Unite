using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WildPokemonInfo", menuName = "Pokemon/New Wild Pokemon")]
public class WildPokemonInfo : ScriptableObject
{
    [SerializeField] private int expYield = 250;
    [SerializeField] private short energyYield = 5;
    [SerializeField] private PokemonBase pokemonBase;

    public int ExpYield { get => expYield; set => expYield = value; }
    public short EnergyYield { get => energyYield; set => energyYield = value; }
    public PokemonBase PokemonBase { get => pokemonBase; set => pokemonBase = value; }
}
