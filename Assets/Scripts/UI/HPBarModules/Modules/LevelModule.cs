using TMPro;
using UnityEngine;

/// <summary>
/// Module for displaying Pokemon level.
/// </summary>
public class LevelModule : HPBarModuleBase
{
    [Header("Level Text Reference")]
    [SerializeField] private TMP_Text levelText;

    [Header("Display Settings")]
    [SerializeField] private string prefix = "";
    [SerializeField] private int levelOffset = 1; // Usually levels start at 1, not 0

    protected override void SubscribeToEvents()
    {
        if (pokemon != null)
        {
            pokemon.OnLevelChange += UpdateUI;
        }
    }

    protected override void UnsubscribeFromEvents()
    {
        if (pokemon != null)
        {
            pokemon.OnLevelChange -= UpdateUI;
        }
    }

    public override void UpdateUI()
    {
        if (pokemon == null || levelText == null) return;
        levelText.text = $"{prefix}{pokemon.CurrentLevel + levelOffset}";
    }
}
