using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpectatorTeamPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text teamName;
    [SerializeField] private Image accent;
    [SerializeField] private RectTransform content;
    public RectTransform Content => content;
    public RectTransform Rect => (RectTransform)transform;

    public void Initialize(string label, Color color)
    {
        teamName.text = label;
        accent.color = color;
    }

    public static void Place(RectTransform rect, Vector2 min, Vector2 max, float inset = 0)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.localScale = Vector3.one;
    }
}
