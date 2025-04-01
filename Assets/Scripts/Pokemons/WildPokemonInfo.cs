using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "WildPokemonInfo", menuName = "Pokemon/New Wild Pokemon")]
public class WildPokemonInfo : ScriptableObject
{
    [SerializeField] private int[] expYield = new int[22];
    [SerializeField] private ushort[] energyYield = new ushort[22];
    [SerializeField] private AssetReference pokemonBase;
    [SerializeField] private AvailableWildPokemons soldierToSpawn;
    [SerializeField] private ObjectiveType objectiveType;
    [TextArea(3, 10)]
    [SerializeField] private string killNotification;

    public int[] ExpYield { get => expYield; set => expYield = value; }
    public ushort[] EnergyYield { get => energyYield; set => energyYield = value; }
    public AssetReference PokemonBase { get => pokemonBase; set => pokemonBase = value; }
    public AvailableWildPokemons SoldierToSpawn { get => soldierToSpawn; set => soldierToSpawn = value; }
    public ObjectiveType ObjectiveType { get => objectiveType; set => objectiveType = value; }
    public string KillNotification { get => killNotification; set => killNotification = value; }
}

public enum ObjectiveType { Zapdos, Drednaw, Rotom, Registeel }