using System.Collections.Generic;
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
    [SerializeField] private HeldItemInfo[] heldItems;
    [SerializeField] private MapInfo[] maps;

    public CharacterInfo[] Characters => characters;
    public WildPokemonInfo[] WildPokemons => wildPokemons;
    public BattleItemAsset[] BattleItems => battleItems;
    public HeldItemInfo[] HeldItems => heldItems;
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

    public CharacterInfo GetCharacterFromID(short characterID)
    {
        if (characterID > characters.Length)
        {
            return characters[0];
        }

        return characters[characterID];
    }

    public short GetCharacterID(CharacterInfo character)
    {
        for (short i = 0; i < characters.Length; i++)
        {
            if (characters[i] == character)
            {
                return i;
            }
        }

        return 0;
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

    public byte[] GetHeldItemsIDs(List<HeldItemInfo> heldItems)
    {
        byte[] heldItemsIDs = new byte[heldItems.Count];

        for (int i = 0; i < heldItems.Count; i++)
        {
            for (int j = 0; j < this.heldItems.Length; j++)
            {
                if (heldItems[i].heldItemID == this.heldItems[j].heldItemID)
                {
                    heldItemsIDs[i] = (byte)j;
                    break;
                }
            }
        }

        return heldItemsIDs;
    }

    public HeldItemInfo GetHeldItemByID(int id)
    {
        if (id > heldItems.Length)
        {
            return heldItems[0];
        }

        return heldItems[id];
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
