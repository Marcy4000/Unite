using TMPro;
using UnityEngine;

/// <summary>
/// Module for displaying player name above HP bar.
/// </summary>
public class NameModule : HPBarModuleBase
{
    [Header("Name Text Reference")]
    [SerializeField] private TMP_Text nameText;

    private string displayName;

    public string DisplayName
    {
        get => displayName;
        set
        {
            displayName = value;
            UpdateUI();
        }
    }

    public override void UpdateUI()
    {
        if (nameText == null) return;
        nameText.text = displayName ?? string.Empty;
    }

    /// <summary>
    /// Set the player name to display.
    /// </summary>
    public void SetPlayerName(string playerName)
    {
        DisplayName = playerName;
    }
}
