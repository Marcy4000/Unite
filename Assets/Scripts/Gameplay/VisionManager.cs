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
        // Also handle additive scene load ordering (Vision.OnEnable can precede
        // this manager's Awake).
        foreach (var vision in FindObjectsByType<Vision>(FindObjectsSortMode.None)) RegisterVision(vision);
        foreach (var controller in FindObjectsByType<VisionController>(FindObjectsSortMode.None)) RegisterController(controller);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

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

    // Per-team simulation query; never use the host's rendered state for an AI.
    public bool IsVisibleToTeam(Vision vision, Team team)
    {
        if (vision == null || !vision.isActiveAndEnabled || !vision.IsVisiblyEligible) return false;
        if (vision.CurrentTeam == team || vision.TemporarilyRevealed) return true;
        foreach (var controller in allControllers)
            if (controller != null && controller.CurrentTeam == team && controller.CanSee(vision)) return true;
        return false;
    }

    private void UpdateVisibility()
    {
        for (int i = allControllers.Count - 1; i >= 0; i--)
            if (allControllers[i] == null) allControllers.RemoveAt(i);
        for (int i = allVisions.Count - 1; i >= 0; i--)
        {
            var vision = allVisions[i];
            if (vision == null) { allVisions.RemoveAt(i); continue; }
            vision.SetVisibility(IsVisibleToTeam(vision, LocalPlayerTeam));
        }
    }


    public void ForceUpdate()
    {
        UpdateVisibility();
    }
}
