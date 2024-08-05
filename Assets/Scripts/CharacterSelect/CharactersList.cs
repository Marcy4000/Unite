using UnityEngine;
using UnityEngine.SceneManagement;

public enum AvailableWildPokemons : short
{
    Tauros,
    Aipom,
    Zapdos,
    Audino,
    StartingAipom,
    Corpish,
    CorpishJungle,
    Lillipup,
    Ludicolo,
    Bouffalant,
    Vespiquen,
    Combee,
    Drednaw,
    Accelgor,
    Altaria,
    BaltoyCenter,
    BaltoyJungle,
    BaltoyLane,
    Bunnelby,
    Escavalier,
    Indeedee,
    StartingBunnelby,
    Swabalu,
    Rayquaza,
    Xatu,
    Registeel,
    Rotom,
    RotomSoldier,
    Regieleki,
    RegielekiSoldier
}


public class CharactersList : MonoBehaviour
{
    public static CharactersList Instance { get; private set; }

    [SerializeField] private CharacterInfo[] characters;
    [SerializeField] private WildPokemonInfo[] wildPokemons;
    [SerializeField] private Sprite[] moveLabels;
    [SerializeField] private BattleItemAsset[] battleItems;
    [SerializeField] private MapInfo[] maps;

    public CharacterInfo[] Characters => characters;
    public WildPokemonInfo[] WildPokemons => wildPokemons;
    public BattleItemAsset[] BattleItems => battleItems;
    public MapInfo[] Maps => maps;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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

    public MapInfo GetCurrentMap()
    {
        foreach (var map in maps)
        {
            if (map.sceneName == SceneManager.GetActiveScene().name)
            {
                return map;
            }
        }

        return null;
    }

    public MapInfo GetCurrentLobbyMap()
    {
        string lobbyMap = LobbyController.Instance.Lobby.Data["SelectedMap"].Value;

        foreach (var map in maps)
        {
            if (map.sceneName == lobbyMap)
            {
                return map;
            }
        }

        return null;
    }
}
