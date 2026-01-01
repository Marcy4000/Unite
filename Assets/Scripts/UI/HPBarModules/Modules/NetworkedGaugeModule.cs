using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Networked gauge module that syncs its value across all clients.
/// Useful for ability gauges that all players need to see.
/// </summary>
public class NetworkedGaugeModule : NetworkedHPBarModule<float>
{
    [Header("Gauge References")]
    [SerializeField] private Image gaugeBar;
    [SerializeField] private Image gaugeBackground;

    [Header("Gauge Settings")]
    [SerializeField] private Color fillColor = Color.yellow;
    [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

    private float maxValue = 1f;

    public float CurrentValue => syncedValue.Value;
    public float MaxValue => maxValue;
    public float NormalizedValue => maxValue > 0 ? syncedValue.Value / maxValue : 0f;

    public override void Initialize(Pokemon pokemon, bool isOwner)
    {
        base.Initialize(pokemon, isOwner);
        ApplyColors();
    }

    private void ApplyColors()
    {
        if (gaugeBar != null)
        {
            gaugeBar.color = fillColor;
        }
        if (gaugeBackground != null)
        {
            gaugeBackground.color = backgroundColor;
        }
    }

    /// <summary>
    /// Set the gauge fill amount (0-1). Will sync to all clients.
    /// </summary>
    public void SetFillAmount(float fillAmount)
    {
        SetValue(Mathf.Clamp01(fillAmount) * maxValue);
    }

    /// <summary>
    /// Set the gauge value with a max value. Will sync to all clients.
    /// </summary>
    public void SetValue(float value, float max)
    {
        maxValue = max;
        SetValue(Mathf.Clamp(value, 0f, max));
    }

    /// <summary>
    /// Set only the current value. Will sync to all clients.
    /// </summary>
    public new void SetValue(float value)
    {
        base.SetValue(Mathf.Clamp(value, 0f, maxValue));
    }

    /// <summary>
    /// Set the max value (local only, not synced).
    /// </summary>
    public void SetMaxValue(float max)
    {
        maxValue = max;
        UpdateUI();
    }

    /// <summary>
    /// Configure gauge colors.
    /// </summary>
    public void SetColors(Color fill, Color background)
    {
        fillColor = fill;
        backgroundColor = background;
        ApplyColors();
    }

    /// <summary>
    /// Configure just the fill color.
    /// </summary>
    public void SetFillColor(Color fill)
    {
        fillColor = fill;
        if (gaugeBar != null)
        {
            gaugeBar.color = fillColor;
        }
    }

    public override void UpdateUI()
    {
        if (gaugeBar == null) return;
        gaugeBar.fillAmount = Mathf.Clamp(NormalizedValue, 0f, 1f);
    }
}
