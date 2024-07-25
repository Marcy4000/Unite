using UnityEngine;
using UnityEngine.AddressableAssets;

public enum PokemonClass { Attacker, Defender, Supporter, AllRounder, Speedster }

[CreateAssetMenu(fileName = "Character", menuName = "Character", order = 1)]
public class CharacterInfo : ScriptableObject
{
    public string pokemonName;
    //public PokemonBase pokemon;
    public AssetReference pokemon;
    public PokemonClass pClass;

    [Space]
    public AssetReferenceGameObject model;

    public Sprite icon;
    public Sprite portrait;
}
