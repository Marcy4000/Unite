using UnityEngine;
using UnityEngine.UI;

// Only controls the lane badge. Pokemon portraits and loadouts remain owned by the card scripts.
public class LaneSelectionBadge : MonoBehaviour
{
    [SerializeField] private Image icon;
    private LaneSelectionNetwork selection;
    private string playerId;

    public void Bind(string id)
    {
        Unsubscribe();
        playerId = id;
        selection = LaneSelectionNetwork.Instance;
        Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (selection == null || !isActiveAndEnabled) return;
        selection.Changed -= Refresh;
        selection.Changed += Refresh;
    }

    private void Unsubscribe()
    {
        if (selection != null) selection.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (icon == null) return;
        var lane = selection != null && selection.IsAvailable ? selection.Layout.FindLane(selection.GetLane(playerId)) : null;
        icon.sprite = lane != null ? lane.icon : null;
        icon.enabled = lane != null && lane.icon != null;
    }
}
