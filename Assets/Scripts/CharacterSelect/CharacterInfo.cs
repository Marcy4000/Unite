using UnityEngine;
using UnityEngine.AddressableAssets;

public enum PokemonClass : byte { Attacker, Defender, Supporter, AllRounder, Speedster }
public enum PokemonDifficulty : byte { Novice, Intermediate, Expert }
public enum PokemonRange : byte { Melee, Ranged }

[CreateAssetMenu(fileName = "Character", menuName = "Character", order = 1)]
public class CharacterInfo : ScriptableObject
{
    public string pokemonName;
    //public PokemonBase pokemon;
    public AssetReference pokemon;
    public PokemonClass pClass;

    [Space]
    public PokemonDifficulty Difficulty;
    public PokemonRange Range;
    public DamageType DamageType;

    [Space]
    public AssetReferenceGameObject model;

    public Sprite icon;
    public Sprite portrait;

    public bool Hidden;
}
