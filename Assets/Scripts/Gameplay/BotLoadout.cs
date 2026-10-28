using UnityEngine;

[CreateAssetMenu(menuName = "Bots/Loadout")]
public class BotLoadout : ScriptableObject
{
    public CharacterInfo pokemon;
    public PokemonBase gameplayPokemon;
    public MoveAsset moveA;
    public MoveAsset moveB;
    public byte[] heldItems = new byte[3];
    public BattleItemAsset battleItem;
    [Min(0)] public float weight = 1;
    public bool enabledForBots = true;

    public bool IsValid => enabledForBots && pokemon != null && !pokemon.Hidden && float.IsFinite(weight) && weight > 0 &&
        moveA != null && moveA.moveType == MoveType.MoveA && moveB != null && moveB.moveType == MoveType.MoveB &&
        CanLearn(moveA) && CanLearn(moveB) && HeldItemsValid() && battleItem != null &&
        (CharactersList.Instance == null || System.Array.IndexOf(CharactersList.Instance.BattleItems, battleItem) >= 0) &&
        battleItem.battleItemType != AvailableBattleItems.SlowSmoke && battleItem.battleItemType != AvailableBattleItems.ShedinjaDoll;

    private bool CanLearn(MoveAsset move)
    {
        if (gameplayPokemon == null || gameplayPokemon.PokemonName != pokemon.pokemonName || gameplayPokemon.LearnableMoves == null) return false;
        foreach (var tier in gameplayPokemon.LearnableMoves)
            foreach (var candidate in tier.moves)
                if (candidate != null && candidate.move == move.move) return true;
        return false;
    }
    private bool HeldItemsValid()
    {
        if (heldItems == null || heldItems.Length != 3) return false;
        for (int i = 0; i < heldItems.Length; i++)
        {
            if (CharactersList.Instance != null && heldItems[i] >= CharactersList.Instance.HeldItems.Length) return false;
            for (int j = 0; j < i; j++) if (heldItems[i] != 0 && heldItems[i] == heldItems[j]) return false;
        }
        return true;
    }
}
