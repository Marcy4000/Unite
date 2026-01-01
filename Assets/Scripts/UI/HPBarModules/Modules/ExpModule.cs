using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Module for displaying EXP bar and stored EXP.
/// Typically used only for player Pokemon.
/// </summary>
public class ExpModule : HPBarModuleBase
{
    [Header("EXP Bar References")]
    [SerializeField] private Image expBar;
    [SerializeField] private Image storedExpBar;

    protected override void SubscribeToEvents()
    {
        if (pokemon != null)
        {
            pokemon.OnExpChange += UpdateUI;
            pokemon.OnLevelChange += UpdateUI;
        }
    }

    protected override void UnsubscribeFromEvents()
    {
        if (pokemon != null)
        {
            pokemon.OnExpChange -= UpdateUI;
            pokemon.OnLevelChange -= UpdateUI;
        }
    }

    public override void UpdateUI()
    {
        if (pokemon == null || expBar == null) return;

        // Check if at max level
        if (pokemon.LocalLevel == pokemon.LevelCap - 1)
        {
            expBar.fillAmount = 1;
            if (storedExpBar != null)
            {
                storedExpBar.fillAmount = 0;
            }
            return;
        }

        float expNeeded = pokemon.BaseStats.GetExpForNextLevel(pokemon.LocalLevel);
        float normalizedExp = pokemon.LocalExp / expNeeded;
        float normalizedStoredExp = pokemon.LocalStoredExp / expNeeded;
        normalizedStoredExp += normalizedExp;

        if (normalizedExp < 0)
        {
            expBar.fillAmount = 1;
        }
        else
        {
            expBar.fillAmount = normalizedExp;
        }

        if (storedExpBar != null)
        {
            storedExpBar.fillAmount = normalizedStoredExp;
        }
    }
}
