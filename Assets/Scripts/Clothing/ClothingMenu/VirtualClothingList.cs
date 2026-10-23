using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Recycles fixed-height wardrobe rows; only visible rows retain icon loads.</summary>
public sealed class VirtualClothingList : MonoBehaviour
{
    private sealed class Row
    {
        public ClothingSelectionItem Item;
        public RectTransform Rect;
        public int Index = -1;
    }

    private ScrollRect scroll;
    private RectTransform viewport;
    private GameObject prefab;
    private ToggleGroup toggleGroup;
    private RectOffset padding;
    private float spacing;
    private Vector2 cellSize;
    private int alignment;
    private readonly List<Row> rows = new List<Row>();
    private readonly Dictionary<int, Row> visible = new Dictionary<int, Row>();
    private IReadOnlyList<ClothingItem> items;
    private Action<int> onSelected;
    private int selectedIndex;
    private Vector2 previousPosition;
    private Vector2 previousViewportSize;
    private float previousWidth;
    private bool refreshRequested;

    public void Initialize(ScrollRect scrollRect, GameObject rowPrefab, ToggleGroup group)
    {
        scroll = scrollRect;
        prefab = rowPrefab;
        toggleGroup = group;
        viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
        var layout = scroll.content.GetComponent<VerticalLayoutGroup>();
        padding = layout != null ? layout.padding : new RectOffset();
        spacing = layout != null ? layout.spacing : 0f;
        alignment = layout != null ? (int)layout.childAlignment % 3 : 1;
        cellSize = ((RectTransform)prefab.transform).rect.size;
        cellSize.y = Mathf.Max(1f, cellSize.y);
        if (layout != null) layout.enabled = false;
        var fitter = scroll.content.GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;
        // Selection lives in the data, not in whichever toggle happens to be visible.
        if (toggleGroup != null) toggleGroup.allowSwitchOff = true;
        scroll.onValueChanged.AddListener(OnScroll);
        foreach (Transform child in scroll.content)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    public void SetItems(IReadOnlyList<ClothingItem> data, int selection, Action<int> selected)
    {
        foreach (var row in rows) Unbind(row);
        visible.Clear();
        items = data;
        selectedIndex = selection;
        onSelected = selected;
        int count = items != null ? items.Count : 0;
        float height = padding.vertical + count * cellSize.y + Mathf.Max(0, count - 1) * spacing;
        scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        scroll.StopMovement();
        var position = scroll.content.anchoredPosition;
        position.y = 0f;
        scroll.content.anchoredPosition = position;
        refreshRequested = true;
        RefreshVisibleRows();
    }

    public static Vector2Int GetVisibleRange(int count, float viewportHeight, float offset,
        float rowHeight, float rowSpacing, float topPadding)
    {
        if (count <= 0) return Vector2Int.zero;
        float stride = Mathf.Max(1f, rowHeight + rowSpacing);
        int first = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(0f, offset) - topPadding) / stride) - 1, 0, count - 1);
        int last = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(0f, offset) + Mathf.Max(0f, viewportHeight) - topPadding) / stride) + 1,
            first + 1, count);
        return new Vector2Int(first, last);
    }

    private void OnScroll(Vector2 position) => refreshRequested = true;

    private void OnEnable() => refreshRequested = true;

    private void OnDisable()
    {
        foreach (var row in rows) Unbind(row);
        visible.Clear();
    }

    private void LateUpdate()
    {
        if (scroll == null) return;
        if (refreshRequested || previousPosition != scroll.content.anchoredPosition
            || previousViewportSize != viewport.rect.size || previousWidth != scroll.content.rect.width)
            RefreshVisibleRows();
    }

    private void RefreshVisibleRows()
    {
        if (scroll == null || !isActiveAndEnabled) return;
        refreshRequested = false;
        previousPosition = scroll.content.anchoredPosition;
        previousViewportSize = viewport.rect.size;
        previousWidth = scroll.content.rect.width;
        var range = GetVisibleRange(items != null ? items.Count : 0, viewport.rect.height,
            previousPosition.y, cellSize.y, spacing, padding.top);
        foreach (var row in rows)
        {
            if (row.Index < 0 || (row.Index >= range.x && row.Index < range.y)) continue;
            visible.Remove(row.Index);
            Unbind(row);
        }
        for (int index = range.x; index < range.y; index++)
        {
            if (!visible.TryGetValue(index, out var row))
            {
                row = FindFreeRow();
                row.Index = index;
                row.Item.SetItem(items[index]);
                var toggle = row.Item.ItemToggle;
                toggle.group = toggleGroup;
                toggle.SetIsOnWithoutNotify(index == selectedIndex);
                row.Item.gameObject.SetActive(true);
                visible.Add(index, row);
            }
            PositionRow(row);
        }
    }

    private Row FindFreeRow()
    {
        foreach (var row in rows)
        {
            if (row.Index < 0) return row;
        }
        var instance = Instantiate(prefab, scroll.content);
        instance.SetActive(false);
        var created = new Row { Item = instance.GetComponent<ClothingSelectionItem>(), Rect = (RectTransform)instance.transform };
        created.Item.ItemToggle.onValueChanged.AddListener(value =>
        {
            if (!value || created.Index < 0 || created.Index == selectedIndex) return;
            selectedIndex = created.Index;
            foreach (var row in rows)
                row.Item.ItemToggle.SetIsOnWithoutNotify(row.Index == selectedIndex);
            onSelected?.Invoke(selectedIndex);
        });
        rows.Add(created);
        return created;
    }

    private void PositionRow(Row row)
    {
        float anchor = alignment * 0.5f;
        row.Rect.anchorMin = row.Rect.anchorMax = new Vector2(anchor, 1f);
        row.Rect.pivot = new Vector2(0.5f, 0.5f);
        row.Rect.sizeDelta = cellSize;
        float x = alignment == 0 ? padding.left + cellSize.x * 0.5f
            : alignment == 2 ? -padding.right - cellSize.x * 0.5f : (padding.left - padding.right) * 0.5f;
        row.Rect.anchoredPosition = new Vector2(x, -padding.top - row.Index * (cellSize.y + spacing) - cellSize.y * 0.5f);
    }

    private static void Unbind(Row row)
    {
        row.Index = -1;
        row.Item.ItemToggle.group = null;
        row.Item.ItemToggle.SetIsOnWithoutNotify(false);
        row.Item.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (scroll != null) scroll.onValueChanged.RemoveListener(OnScroll);
    }
}
