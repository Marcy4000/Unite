using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "WildPokemonInfo", menuName = "Pokemon/New Wild Pokemon")]
public class WildPokemonInfo : ScriptableObject
{
    [SerializeField] private int expYield = 250;
    [SerializeField] private ushort energyYield = 5;
    [SerializeField] private AssetReference pokemonBase;
    [SerializeField] private AvailableWildPokemons soldierToSpawn;
    [SerializeField] private ObjectiveType objectiveType;

    public int ExpYield { get => expYield; set => expYield = value; }
    public ushort EnergyYield { get => energyYield; set => energyYield = value; }
    public AssetReference PokemonBase { get => pokemonBase; set => pokemonBase = value; }
    public AvailableWildPokemons SoldierToSpawn { get => soldierToSpawn; set => soldierToSpawn = value; }
    public ObjectiveType ObjectiveType { get => objectiveType; set => objectiveType = value; }
}

public enum ObjectiveType { Zapdos, Drednaw, Rotom, Registeel }