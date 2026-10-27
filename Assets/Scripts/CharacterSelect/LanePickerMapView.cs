using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class LanePickerMapView : MonoBehaviour
{
    public struct PlayerMarker
    {
        public string PlayerId;
        public string LaneId;
        public Sprite Portrait;
    }

    [SerializeField] private Image background;
    [SerializeField] private Image highlightTemplate;
    [SerializeField] private RectTransform markerTemplate;
    [SerializeField] private Sprite localPlayerFrame;
    [SerializeField] private Sprite teammateFrame;
    [SerializeField] private bool expanded;
    [SerializeField, Min(0.1f)] private float markerScale = 1.25f;

    private readonly Dictionary<string, Image> highlights = new Dictionary<string, Image>();
    private readonly Dictionary<string, RectTransform> markers = new Dictionary<string, RectTransform>();
    private LanePickerLayout layout;
    private int capacity;

    public void Initialize(LanePickerLayout mapLayout, int teamCapacity)
    {
        ClearGenerated();
        layout = mapLayout;
        capacity = Mathf.Max(2, teamCapacity);
        background.sprite = expanded ? layout.expandedMap : layout.compactMap;
        highlightTemplate.gameObject.SetActive(false);
        markerTemplate.gameObject.SetActive(false);
        foreach (var lane in layout.lanes)
        {
            var visual = expanded ? lane.expanded : lane.compact;
            var highlight = Instantiate(highlightTemplate, background.transform);
            highlight.name = lane.id + " Highlight";
            highlight.sprite = visual.highlight;
            highlight.rectTransform.anchorMin = highlight.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            highlight.rectTransform.anchoredPosition = visual.highlightPosition;
            highlight.rectTransform.sizeDelta = visual.highlightSize;
            highlight.raycastTarget = false;
            highlights.Add(lane.id, highlight);
        }
    }

    public void Refresh(string selectedLane, string localPlayerId, IReadOnlyList<PlayerMarker> players)
    {
        foreach (var pair in highlights) pair.Value.gameObject.SetActive(pair.Key == selectedLane);
        foreach (var marker in markers.Values) marker.gameObject.SetActive(false);

        foreach (var lane in layout.lanes)
        {
            int slot = 0;
            foreach (var player in players.Where(player => player.LaneId == lane.id && player.Portrait != null))
            {
                if (!markers.TryGetValue(player.PlayerId, out var marker))
                {
                    marker = Instantiate(markerTemplate, background.transform);
                    marker.name = "Lane Player " + player.PlayerId;
                    marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
                    markers.Add(player.PlayerId, marker);
                }
                var visual = expanded ? lane.expanded : lane.compact;
                marker.anchoredPosition = visual.GetMarkerPosition(slot++, capacity);
                marker.localRotation = Quaternion.identity;
                marker.localScale = Vector3.one * markerScale;
                marker.Find("PokemonIcon").GetComponent<Image>().sprite = player.Portrait;
                marker.Find("Frame").GetComponent<Image>().sprite = player.PlayerId == localPlayerId ? localPlayerFrame : teammateFrame;
                marker.SetAsLastSibling();
                marker.gameObject.SetActive(true);
            }
        }

        foreach (var id in markers.Keys.Where(id => !players.Any(player => player.PlayerId == id)).ToArray())
        {
            Destroy(markers[id].gameObject);
            markers.Remove(id);
        }
    }

    private void ClearGenerated()
    {
        foreach (var highlight in highlights.Values) Destroy(highlight.gameObject);
        foreach (var marker in markers.Values) Destroy(marker.gameObject);
        highlights.Clear();
        markers.Clear();
    }
}
