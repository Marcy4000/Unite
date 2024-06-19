using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharactersList : MonoBehaviour
{
    public static CharactersList instance { get; private set; }

    [SerializeField] private CharacterInfo[] characters;
    [SerializeField] private WildPokemonInfo[] wildPokemons;
    [SerializeField] private Sprite[] backgrounds;
    [SerializeField] private Sprite[] moveLabels;
    [SerializeField] private BattleItemAsset[] battleItems;

    public CharacterInfo[] Characters => characters;
    public WildPokemonInfo[] WildPokemons => wildPokemons;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public CharacterInfo GetCharacterFromString(string characterName)
    {
        foreach (var character in characters)
        {
            if (character.pokemonName == characterName)
            {
                return character;
            }
        }

        return null;
    }

    public Sprite GetBackgroundFromClass(PokemonClass pClass)
    {
        switch (pClass)
        {
            case PokemonClass.Attacker:
                return backgrounds[0];
            case PokemonClass.Defender:
                return backgrounds[1];
            case PokemonClass.Supporter:
                return backgrounds[2];
            case PokemonClass.AllRounder:
                return backgrounds[3];
            case PokemonClass.Speedster:
                return backgrounds[4];
            default:
                return null;
        }
    }

    public Sprite GetMoveLabel(MoveLabels label)
    {
        return moveLabels[(int)label-1];
    }

    public BattleItemAsset GetBattleItemByID(int id)
    {
        if (id > battleItems.Length)
        {
            return battleItems[0];
        }

        return battleItems[id];
    }
}
