using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation-only discrete gauge. Capacity and state are supplied by gameplay.</summary>
public class SegmentedGaugeModule : GaugeModule
{
    [SerializeField] private RectTransform separatorsHolder;
    [SerializeField] private Image separatorPrefab;
    [SerializeField, Min(0.001f)] private float separatorWidth = 0.025f;
    private readonly List<Image> separators = new List<Image>();
    public int Capacity { get; private set; } = 1;
    public int Steps { get; private set; }
    public int SeparatorCount => Mathf.Max(0, Capacity - 1);

    public void SetSteps(int steps, int capacity)
    {
        Capacity = Mathf.Max(1, capacity);
        Steps = Mathf.Clamp(steps, 0, Capacity);
        SetValue(Steps, Capacity);
        LayoutSeparators();
    }

    private void LayoutSeparators()
    {
        if (separatorsHolder == null || separatorPrefab == null) return;
        while (separators.Count < SeparatorCount)
        {
            var image = Instantiate(separatorPrefab, separatorsHolder);
            image.name = "StepSeparator";
            image.color = Color.black;
            image.raycastTarget = false;
            separators.Add(image);
        }
        for (int i = 0; i < separators.Count; i++)
        {
            var image = separators[i];
            image.gameObject.SetActive(i < SeparatorCount);
            if (i >= SeparatorCount) continue;
            float x = (i + 1f) / Capacity;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(x, 0);
            rect.anchorMax = new Vector2(x, 1);
            rect.sizeDelta = new Vector2(separatorWidth, 0);
            rect.anchoredPosition = Vector2.zero;
        }
    }

    public override void Initialize(Pokemon pokemon, bool isOwner)
    {
        base.Initialize(pokemon, isOwner);
        LayoutSeparators();
    }

    public override void Cleanup()
    {
        SetSteps(0, Capacity);
        base.Cleanup();
    }
}
