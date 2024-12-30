using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MovesMenu : MonoBehaviour
{
    [SerializeField] private GameObject levelButtonPrefab, moveInfoPrefab;
    [SerializeField] private Transform levelButtonParent, moveInfoParent;
    [SerializeField] private ToggleGroup levelButtonToggleGroup;
    [SerializeField] private MessageBox messageBox;

    private List<MovesLevelButton> levelButtons = new List<MovesLevelButton>();

    private PokemonBase currentPokemon;

    public void Initialize(PokemonBase pokemon)
    {
        foreach (var button in levelButtons)
        {
            Destroy(button.gameObject);
        }
        levelButtons.Clear();

        currentPokemon = pokemon;
        for (int i = 0; i < pokemon.LearnableMoves.Length; i++)
        {
            if (pokemon.LearnableMoves[i].isUpgraded || i == 1)
                continue;

            MovesLevelButton levelButton = Instantiate(levelButtonPrefab, levelButtonParent).GetComponent<MovesLevelButton>();
            levelButton.Initialize(GetBestEvolutionPortrait(pokemon, pokemon.LearnableMoves[i].level), pokemon.LearnableMoves[i].level, i, levelButtonToggleGroup);
            levelButton.OnSelected += OnLevelButtonSelected;
            levelButton.Toggle.isOn = i == 0;
            levelButtons.Add(levelButton);
        }

        ShowMoveInfo(0);
        messageBox.Hide();
    }

    private Sprite GetBestEvolutionPortrait(PokemonBase pokemon, int moveLevel)
    {
        PokemonEvolution evolution = pokemon.Evolutions[0];

        for (int i = pokemon.Evolutions.Length-1; i > 0; i--)
        {
            if (pokemon.Evolutions[i].level <= moveLevel)
            {
                evolution = pokemon.Evolutions[i];
                break;
            }
        }

        return evolution.newSprite;
    }

    private void OnLevelButtonSelected(int movesIndex)
    {
        ShowMoveInfo(movesIndex);
    }

    private void ShowMoveInfo(int movesIndex)
    {
        foreach (Transform child in moveInfoParent)
        {
            Destroy(child.gameObject);
        }

        if (currentPokemon.LearnableMoves.Length <= movesIndex)
            return;

        foreach (var move in currentPokemon.LearnableMoves[movesIndex].moves)
        {
            MoveInfoUI moveInfoUI = Instantiate(moveInfoPrefab, moveInfoParent).GetComponent<MoveInfoUI>();
            moveInfoUI.Initialize(move, ShowMessageBox);
        }
    }

    private void ShowMessageBox(MoveAsset move)
    {
        Announcement announcement = new Announcement
        {
            title = move.moveName,
            message = move.description,
        };

        messageBox.SetAnnouncement(announcement);
        messageBox.Show();
    }
}
