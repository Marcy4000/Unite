using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Opt-in, presentation-only boosted attack counter. Displays earned normal charges
/// in white and ready/stored boosted attacks in yellow. Attack code supplies the
/// counts explicitly, including any forced boosts, resets, or evolution changes.
/// </summary>
public class BoostedAttackCounterModule : HPBarModuleBase
{
    [Header("Indicators")]
    [SerializeField] private Sprite indicatorSprite;
    [SerializeField, Min(0)] private int maxNormalCharges = 2;
    [SerializeField] private Vector2 iconSize = new Vector2(0.14f, 0.11f);
    [SerializeField, Min(0f)] private float iconSpacing = 0.025f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color boostedColor = new Color(1f, 1f, 0f, 1f);

    private readonly List<Image> icons = new List<Image>();
    private int normalCharges;
    private int boostedCharges;

    public int MaxNormalCharges => maxNormalCharges;
    public int NormalCharges => normalCharges;
    public bool BoostedReady => boostedCharges > 0;
    public int BoostedCharges => boostedCharges;
    public int VisibleIndicatorCount => normalCharges + boostedCharges;

    private void Awake()
    {
        UpdateUI();
    }

    /// <summary>Capacity excludes the additional boosted-ready icon.</summary>
    public void Configure(int normalChargeCapacity)
    {
        maxNormalCharges = Mathf.Max(0, normalChargeCapacity);
        normalCharges = Mathf.Clamp(normalCharges, 0, maxNormalCharges);
        UpdateUI();
    }

    /// <summary>
    /// Sets the complete display state. Readiness is independent of the white charge
    /// count, so a granted boost can be shown even with no normal charges.
    /// </summary>
    public void SetState(int normalChargeCount, bool isBoostedReady)
    {
        SetChargeCounts(normalChargeCount, isBoostedReady ? 1 : 0);
    }

    /// <summary>For stored empowered attacks, e.g. Emboar's three Fire Pledge hits.</summary>
    public void SetChargeCounts(int normalChargeCount, int boostedChargeCount)
    {
        int clamped = Mathf.Clamp(normalChargeCount, 0, maxNormalCharges);
        int ready = Mathf.Max(0, boostedChargeCount);
        if (normalCharges == clamped && boostedCharges == ready) return;

        normalCharges = clamped;
        boostedCharges = ready;
        UpdateUI();
    }

    public void SetNormalCharges(int count)
    {
        SetChargeCounts(count, boostedCharges);
    }

    public void SetBoostedReady(bool ready)
    {
        SetState(normalCharges, ready);
    }

    public void ResetCounter()
    {
        SetState(0, false);
    }

    public override void UpdateUI()
    {
        RectTransform rect = RectTransform;
        if (rect == null) return;

        maxNormalCharges = Mathf.Max(0, maxNormalCharges);
        normalCharges = Mathf.Clamp(normalCharges, 0, maxNormalCharges);
        float spacing = Mathf.Max(0f, iconSpacing);
        Vector2 size = new Vector2(Mathf.Max(0.001f, iconSize.x), Mathf.Max(0.001f, iconSize.y));

        // Pool indicators; changing state never destroys/recreates the row.
        while (icons.Count < Mathf.Max(maxNormalCharges + 1, VisibleIndicatorCount))
        {
            var iconObject = new GameObject("Charge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.layer = gameObject.layer;
            iconObject.transform.SetParent(rect, false);
            Image image = iconObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            icons.Add(image);
        }

        int visible = VisibleIndicatorCount;
        float width = visible > 0 ? visible * size.x + (visible - 1) * spacing : 0f;
        rect.pivot = new Vector2(0f, 1f);
        rect.SetSizeWithCurrentAnchors(UnityEngine.RectTransform.Axis.Horizontal, width);
        rect.SetSizeWithCurrentAnchors(UnityEngine.RectTransform.Axis.Vertical, size.y);

        for (int i = 0; i < icons.Count; i++)
        {
            Image image = icons[i];
            bool show = i < visible;
            image.gameObject.SetActive(show);
            if (!show) continue;

            image.sprite = indicatorSprite;
            image.color = i >= normalCharges ? boostedColor : normalColor;
            RectTransform iconRect = image.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.sizeDelta = size;
            iconRect.anchoredPosition = new Vector2(i * (size.x + spacing), 0f);
        }
    }

    public override void Cleanup()
    {
        ResetCounter();
        base.Cleanup();
    }
}
