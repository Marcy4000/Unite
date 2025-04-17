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

            bool visible = false;

            if (vision.CurrentTeam == LocalPlayerTeam)
            {
                vision.SetVisibility(true);
                continue;
            }

            foreach (var controller in allControllers)
            {
                if (!controller.IsEnabled || controller.IsBlinded)
                    continue;

                if (controller.TeamToIgnore == vision.CurrentTeam)
                {
                    visible = true;
                    break;
                }

                if (controller.ContainsVision(vision) &&
                    (!vision.IsInBush || vision.IsVisibleInBush.Contains(controller.gameObject.GetInstanceID())))
                {
                    visible = true;
                    break;
                }
            }

            vision.SetVisibility(visible);
        }
    }

    public void ForceUpdate()
    {
        UpdateVisibility();
    }
}
