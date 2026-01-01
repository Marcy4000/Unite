using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Networked module for displaying ability stacks/charges.
/// Syncs stack count across all clients.
/// </summary>
public class NetworkedStackModule : NetworkedHPBarModule<int>
{
    [Header("Stack Container")]
    [SerializeField] private RectTransform stackContainer;

    [Header("Stack Icon Prefab")]
    [SerializeField] private GameObject stackIconPrefab;

    [Header("Stack Settings")]
    [SerializeField] private int maxStacks = 3;
    [SerializeField] private float iconSpacing = 5f;

    [Header("Stack Colors")]
    [SerializeField] private Color activeColor = Color.yellow;
    [SerializeField] private Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

    private List<Image> stackIcons = new List<Image>();

    public int CurrentStacks => syncedValue.Value;
    public int MaxStacks => maxStacks;

    public override void Initialize(Pokemon pokemon, bool isOwner)
    {
        base.Initialize(pokemon, isOwner);
        CreateStackIcons();
    }

    private void CreateStackIcons()
    {
        // Clear existing icons
        foreach (var icon in stackIcons)
        {
            if (icon != null)
            {
                Destroy(icon.gameObject);
            }
        }
        stackIcons.Clear();

        if (stackContainer == null || stackIconPrefab == null) return;

        // Create new icons
        for (int i = 0; i < maxStacks; i++)
        {
            GameObject iconObj = Instantiate(stackIconPrefab, stackContainer);
            Image iconImage = iconObj.GetComponent<Image>();

            if (iconImage != null)
            {
                stackIcons.Add(iconImage);
                iconImage.color = inactiveColor;
            }

            // Position the icon
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            if (iconRect != null)
            {
                iconRect.anchoredPosition = new Vector2(i * (iconRect.sizeDelta.x + iconSpacing), 0);
            }
        }
    }

    /// <summary>
    /// Set the number of active stacks. Syncs to all clients.
    /// </summary>
    public void SetStacks(int stacks)
    {
        SetValue(Mathf.Clamp(stacks, 0, maxStacks));
    }

    /// <summary>
    /// Add one stack. Syncs to all clients.
    /// </summary>
    public void AddStack()
    {
        SetStacks(syncedValue.Value + 1);
    }

    /// <summary>
    /// Remove one stack. Syncs to all clients.
    /// </summary>
    public void RemoveStack()
    {
        SetStacks(syncedValue.Value - 1);
    }

    /// <summary>
    /// Set the maximum number of stacks and rebuild icons.
    /// </summary>
    public void SetMaxStacks(int max)
    {
        maxStacks = max;
        if (syncedValue.Value > max)
        {
            SetValue(max);
        }
        CreateStackIcons();
        UpdateUI();
    }

    /// <summary>
    /// Configure stack colors.
    /// </summary>
    public void SetColors(Color active, Color inactive)
    {
        activeColor = active;
        inactiveColor = inactive;
        UpdateUI();
    }

    public override void UpdateUI()
    {
        for (int i = 0; i < stackIcons.Count; i++)
        {
            if (stackIcons[i] != null)
            {
                stackIcons[i].color = i < syncedValue.Value ? activeColor : inactiveColor;
            }
        }
    }

    public override void Cleanup()
    {
        foreach (var icon in stackIcons)
        {
            if (icon != null)
            {
                Destroy(icon.gameObject);
            }
        }
        stackIcons.Clear();
        base.Cleanup();
    }
}
