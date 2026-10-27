using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LanePickerOption : MonoBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    private string laneId;
    private LaneSelectionNetwork selection;

    public void Initialize(LaneDefinition lane, LaneSelectionNetwork state)
    {
        laneId = lane.id;
        selection = state;
        label.text = lane.displayName;
        icon.sprite = lane.icon;
        toggle.group = null; // Exclusivity comes from shared state; switching off must remain possible.
        toggle.SetIsOnWithoutNotify(false);
        toggle.onValueChanged.AddListener(OnValueChanged);
    }

    private void OnValueChanged(bool selected)
    {
        if (selection == null) return;
        if (selected) selection.SelectLane(laneId);
        else if (selection.GetLane(LobbyController.Instance.Player.Id) == laneId) selection.SelectLane(string.Empty);
    }

    public void Refresh(string selectedLane, bool interactable)
    {
        toggle.SetIsOnWithoutNotify(selectedLane == laneId);
        toggle.interactable = interactable;
    }
}
