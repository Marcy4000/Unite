using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LanePickerLayout", menuName = "Character Select/Lane Picker Layout")]
public class LanePickerLayout : ScriptableObject
{
    public Sprite compactMap;
    public Sprite expandedMap;
    public LaneDefinition[] lanes =
    {
        new LaneDefinition { id = "top", displayName = "Top Path" },
        new LaneDefinition { id = "mid", displayName = "Central Area" },
        new LaneDefinition { id = "bottom", displayName = "Bottom Path" }
    };

    public LaneDefinition FindLane(string id)
    {
        if (string.IsNullOrEmpty(id) || lanes == null) return null;
        return Array.Find(lanes, lane => lane != null && lane.id == id);
    }
}

[Serializable]
public class LaneDefinition
{
    public string id;
    public string displayName;
    public Sprite icon;
    public LaneMapLayout compact = new LaneMapLayout();
    public LaneMapLayout expanded = new LaneMapLayout();
}

[Serializable]
public class LaneMapLayout
{
    public Sprite highlight;
    public Vector2 highlightPosition;
    public Vector2 highlightSize;
    [Tooltip("Quadratic Bezier path, in map-local UI coordinates. Keep the control point on the line for a straight lane.")]
    public Vector2 markerStart;
    public Vector2 markerControl;
    public Vector2 markerEnd;

    public Vector2 GetMarkerPosition(int index, int capacity)
    {
        float t = Mathf.Clamp01(index / (float)Mathf.Max(1, capacity - 1));
        float inverse = 1f - t;
        return inverse * inverse * markerStart + 2f * inverse * t * markerControl + t * t * markerEnd;
    }
}
