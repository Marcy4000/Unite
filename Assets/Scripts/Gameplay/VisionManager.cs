using System.Collections.Generic;
using UnityEngine;

public class VisionManager : MonoBehaviour
{
    public static VisionManager Instance { get; private set; }

    private List<Vision> allVisions = new();
    private List<VisionController> allControllers = new();

    public Team LocalPlayerTeam { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LocalPlayerTeam = LobbyController.Instance.GetLocalPlayerTeam();
    }

    public void RegisterVision(Vision vision)
    {
        if (!allVisions.Contains(vision))
            allVisions.Add(vision);
    }

    public void UnregisterVision(Vision vision)
    {
        allVisions.Remove(vision);
    }

    public void RegisterController(VisionController controller)
    {
        if (!allControllers.Contains(controller))
            allControllers.Add(controller);
    }

    public void UnregisterController(VisionController controller)
    {
        allControllers.Remove(controller);
    }

    private void LateUpdate()
    {
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        foreach (var vision in allVisions)
        {
            if (!vision.IsVisiblyEligible)
            {
                vision.SetVisibility(false);
                continue;
            }

            // Always visible if on the same team as the local player
            if (vision.CurrentTeam == LocalPlayerTeam)
            {
                vision.SetVisibility(true);
                continue;
            }

            bool isVisible = false;

            foreach (var controller in allControllers)
            {
                // Only vision controllers from the local player's team can reveal enemies
                if (!controller.IsEnabled || controller.IsBlinded || controller.CurrentTeam != LocalPlayerTeam)
                    continue;

                // Temporarily revealed enemies are always visible
                if (vision.TemporarilyRevealed)
                {
                    isVisible = true;
                    break;
                }

                // If controller sees the vision and it is not hidden in a bush
                if (controller.ContainsVision(vision) &&
                    (!vision.IsInBush || vision.IsVisibleInBush.Contains(controller.gameObject.GetInstanceID())))
                {
                    isVisible = true;
                    break;
                }
            }

            vision.SetVisibility(isVisible);
        }
    }


    public void ForceUpdate()
    {
        UpdateVisibility();
    }
}
