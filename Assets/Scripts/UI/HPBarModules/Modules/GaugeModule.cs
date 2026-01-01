using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic gauge module for displaying ability cooldowns, charges, or other fill-based values.
/// Can be instantiated multiple times for Pokemon with multiple gauges.
/// This is a local-only module - use NetworkedGaugeModule for synced values.
/// </summary>
public class GaugeModule : HPBarModuleBase
{
    [Header("Gauge References")]
    [SerializeField] private Image gaugeBar;
    [SerializeField] private Image gaugeBackground;

    [Header("Gauge Settings")]
    [SerializeField] private Color fillColor = Color.yellow;
    [SerializeField] private Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

    private float currentValue;
    private float maxValue = 1f;

    public float CurrentValue => currentValue;
    public float MaxValue => maxValue;
    public float NormalizedValue => maxValue > 0 ? currentValue / maxValue : 0f;

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
    /// Set the gauge fill amount (0-1).
    /// </summary>
    public void SetFillAmount(float fillAmount)
    {
        currentValue = Mathf.Clamp01(fillAmount) * maxValue;
        UpdateUI();
    }

    /// <summary>
    /// Set the gauge value with a max value.
    /// </summary>
    public void SetValue(float value, float max)
    {
        maxValue = max;
        currentValue = Mathf.Clamp(value, 0f, max);
        UpdateUI();
    }

    /// <summary>
    /// Set only the current value (keeps existing max).
    /// </summary>
    public void SetValue(float value)
    {
        currentValue = Mathf.Clamp(value, 0f, maxValue);
        UpdateUI();
    }

    /// <summary>
    /// Set the max value.
    /// </summary>
    public void SetMaxValue(float max)
    {
        maxValue = max;
        currentValue = Mathf.Clamp(currentValue, 0f, max);
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
