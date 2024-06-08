using UnityEngine;

[CreateAssetMenu(fileName = "Pokemon", menuName = "Pokemon/Create New Pokemon")]
public class PokemonBase : ScriptableObject
{
    [SerializeField] string pokemonName;

    [TextArea]
    [SerializeField] string description;

    [SerializeField] int[] maxHp = new int[15];
    [SerializeField] int[] attack = new int[15];
    [SerializeField] int[] defense = new int[15];
    [SerializeField] int[] spAttack = new int[15];
    [SerializeField] int[] spDefense = new int[15];
    [SerializeField] int[] critRate = new int[15];
    [SerializeField] int[] cdr = new int[15];
    [SerializeField] int[] lifeSteal = new int[15];
    [SerializeField] float[] atkSpeed = new float[15];
    [SerializeField] int[] speed = new int[15];

    [SerializeField] AvailablePassives passive;

    [SerializeField] LearnableMove[] learnableMoves;

    [SerializeField] PokemonEvolution[] evolutions;

    public string PokemonName { get { return pokemonName; } }
    public string Description { get { return description; } }

    public int[] MaxHp { get { return maxHp; } }
    public int[] Attack { get { return attack; } }
    public int[] Defense { get { return defense; } }
    public int[] SpAttack { get { return spAttack; } }
    public int[] SpDefense { get { return spDefense; } }
    public int[] CritRate { get { return critRate; } }
    public int[] Cdr { get { return cdr; } }
    public int[] LifeSteal { get { return lifeSteal; } }
    public float[] AtkSpeed { get { return atkSpeed; } }
    public int[] Speed { get { return speed; } }

    public AvailablePassives Passive { get { return passive; } }

    public LearnableMove[] LearnableMoves { get { return learnableMoves; } }

    public PokemonEvolution[] Evolutions { get { return evolutions; } }

    public int GetExpForNextLevel(int level)
    {
        switch (level)
        {
            case 0:
                return 100;
            case 1:
                return 100;
            case 2:
                return 400;
            case 3:
                return 500;
            case 4:
                return 650;
            case 5:
                return 650;
            case 6:
                return 750;
            case 7:
                return 870;
            case 8:
                return 1080;
            case 9:
                return 1290;
            case 10:
                return 1550;
            case 11:
                return 1860;
            case 12:
                return 2230;
            case 13:
                return 2670;
            default:
                return -1;
        }
    }

    public bool IsLevelBeforeEvo(int level)
    {
        foreach (PokemonEvolution evolution in evolutions)
        {
            if (evolution.level-1 == level)
            {
                return true;
            }
        }
        return false;
    }

    public PokemonEvolution IsNewEvoLevel(int level)
    {
        foreach (PokemonEvolution evolution in evolutions)
        {
            if (evolution.level == level)
            {
                return evolution;
            }
        }
        return null;
    }
}

public enum PokemonType
{
    Player,
    Wild,
    Objective
}

[System.Serializable]
public class LearnableMove
{
    public MoveAsset[] moves;
    public int level;
}