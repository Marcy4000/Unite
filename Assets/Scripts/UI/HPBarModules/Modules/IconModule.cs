using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Module for displaying a single icon (buff, status, or ability indicator).
/// Can be shown/hidden dynamically.
/// </summary>
public class IconModule : HPBarModuleBase
{
    [Header("Icon References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;

    [Header("Icon Settings")]
    [SerializeField] private Sprite defaultIcon;
    [SerializeField] private Color iconColor = Color.white;
    [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

    public Sprite CurrentIcon => iconImage != null ? iconImage.sprite : null;

    public override void Initialize(Pokemon pokemon, bool isOwner)
    {
        base.Initialize(pokemon, isOwner);
        ApplySettings();
    }

    private void ApplySettings()
    {
        if (iconImage != null)
        {
            iconImage.color = iconColor;
            if (defaultIcon != null)
            {
                iconImage.sprite = defaultIcon;
            }
        }
        if (backgroundImage != null)
        {
            backgroundImage.color = backgroundColor;
        }
    }

    /// <summary>
    /// Set the icon sprite.
    /// </summary>
    public void SetIcon(Sprite icon)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
        }
    }

    /// <summary>
    /// Set the icon color.
    /// </summary>
    public void SetIconColor(Color color)
    {
        iconColor = color;
        if (iconImage != null)
        {
            iconImage.color = color;
        }
    }

    /// <summary>
    /// Set the background color.
    /// </summary>
    public void SetBackgroundColor(Color color)
    {
        backgroundColor = color;
        if (backgroundImage != null)
        {
            backgroundImage.color = color;
        }
    }

    /// <summary>
    /// Show the icon.
    /// </summary>
    public void Show()
    {
        SetVisibility(true);
    }

    /// <summary>
    /// Hide the icon.
    /// </summary>
    public void Hide()
    {
        SetVisibility(false);
    }

    public override void UpdateUI()
    {
        // Icon module doesn't need periodic updates
        // Use SetIcon, SetVisibility to change state
    }
}
