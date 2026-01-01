using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Module for displaying energy (Aeos Energy) count.
/// Can be configured for player or wild Pokemon display styles.
/// </summary>
public class EnergyModule : HPBarModuleBase
{
    [Header("Energy References")]
    [SerializeField] private Image energyBackground;
    [SerializeField] private TMP_Text energyText;

    [Header("Team Sprites")]
    [SerializeField] private Sprite allyEnergyBG;
    [SerializeField] private Sprite enemyEnergyBG;

    private ushort currentEnergy;

    /// <summary>
    /// Initialize energy display for player Pokemon.
    /// </summary>
    /// <param name="team">The team this Pokemon belongs to.</param>
    /// <param name="localPlayerTeam">The local player's team.</param>
    /// <param name="hideForOwner">Whether to hide for the owning player (usually true).</param>
    public void InitializeForPlayer(Team team, Team localPlayerTeam, bool hideForOwner = true)
    {
        if (hideForOwner && isOwner)
        {
            SetVisibility(false);
            return;
        }

        if (energyBackground != null)
        {
            if (team != localPlayerTeam)
            {
                energyBackground.sprite = enemyEnergyBG;
            }
            else
            {
                energyBackground.sprite = allyEnergyBG;
            }
        }

        UpdateEnergyAmount(0);
    }

    /// <summary>
    /// Initialize energy display for wild Pokemon.
    /// </summary>
    /// <param name="initialAmount">Initial energy amount.</param>
    /// <param name="hideUI">Whether to hide the energy display.</param>
    public void InitializeForWild(ushort initialAmount, bool hideUI = false)
    {
        if (hideUI)
        {
            SetVisibility(false);
            return;
        }

        UpdateEnergyAmount(initialAmount);
    }

    /// <summary>
    /// Update the displayed energy amount.
    /// </summary>
    public void UpdateEnergyAmount(ushort amount)
    {
        currentEnergy = amount;
        UpdateUI();
    }

    public override void UpdateUI()
    {
        if (energyText == null) return;
        energyText.text = currentEnergy.ToString();
    }

    public override void SetVisibility(bool visible)
    {
        if (energyBackground != null)
        {
            energyBackground.gameObject.SetActive(visible);
        }
        if (energyText != null)
        {
            energyText.gameObject.SetActive(visible);
        }
    }
}
