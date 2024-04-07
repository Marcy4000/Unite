using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PokemonClass { Attacker, Defender, Supporter, AllRounder, Speedster }

[CreateAssetMenu(fileName = "Character", menuName = "Character", order = 1)]
public class CharacterInfo : ScriptableObject
{
    public string pokemonName;
    public PokemonBase pokemon;
    public PokemonClass pClass;

    [Space]
    public GameObject model;

    public Sprite icon;
    public Sprite portrait;
}
