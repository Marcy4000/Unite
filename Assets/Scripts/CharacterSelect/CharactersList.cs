using System.Collections.Generic;
using UnityEngine;

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
    RegielekiSoldier,
    Zapdos500Points
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
        if (characterID > characters.Length || characterID < 0)
        {
            return null;
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

        return -1;
    }

    public Sprite GetMoveLabel(MoveLabels label)
    {
        return moveLabels[(int)label-1];
    }

    public BattleItemAsset GetBattleItemByID(int id)
    {
        if (id > battleItems.Length)
        {
            return null;
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

    public MapInfo GetCurrentLobbyMap()
    {
        short lobbyMapId = NumberEncoder.FromBase64<short>(LobbyController.Instance.Lobby.Data["SelectedMap"].Value);

        if (lobbyMapId < maps.Length)
        {
            return maps[lobbyMapId];
        }

        return null;
    }

    public short GetMapID(MapInfo map)
    {
        for (short i = 0; i < maps.Length; i++)
        {
            if (maps[i] == map)
            {
                return i;
            }
        }

        return -1;
    }

    public MapInfo GetMapFromID(short mapID)
    {
        if (mapID > maps.Length)
        {
            return maps[0];
        }

        return maps[mapID];
    }
}
