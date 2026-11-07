using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class OpenWorldStageTrackButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private string selectedPrefix = "▶  ";
    public int TrackIndex { get; private set; }
    private UnityAction selectAction;

    public void Initialize(int trackIndex, UnityAction onSelect)
    {
        Clear();
        TrackIndex = trackIndex;
        selectAction = onSelect;
        button.onClick.AddListener(selectAction);
    }

    public void Refresh(string displayName, bool selected, bool canSelect)
    {
        label.text = (selected ? selectedPrefix : "") + displayName;
        button.interactable = canSelect && !selected;
    }

    public void Clear()
    {
        if (button != null && selectAction != null) button.onClick.RemoveListener(selectAction);
        selectAction = null;
    }

    private void OnDestroy() => Clear();
}
