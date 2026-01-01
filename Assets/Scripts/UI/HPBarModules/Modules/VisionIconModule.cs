using UnityEngine;

/// <summary>
/// Module for displaying bush/vision indicator icon.
/// </summary>
public class VisionIconModule : HPBarModuleBase
{
    [Header("Eye Icon Reference")]
    [SerializeField] private GameObject eyeIcon;

    private Vision vision;

    public override void Initialize(Pokemon pokemon, bool isOwner)
    {
        base.Initialize(pokemon, isOwner);
        
        // Default to hidden
        if (eyeIcon != null)
        {
            eyeIcon.SetActive(false);
        }
    }

    /// <summary>
    /// Assign Vision component to track bush state.
    /// </summary>
    public void AssignVision(Vision vision)
    {
        if (this.vision != null)
        {
            this.vision.OnBushChanged -= OnBushChanged;
        }

        this.vision = vision;

        if (vision != null)
        {
            vision.OnBushChanged += OnBushChanged;
        }
    }

    private void OnBushChanged(GameObject currentBush)
    {
        if (eyeIcon != null)
        {
            eyeIcon.SetActive(currentBush != null);
        }
    }

    public override void UpdateUI()
    {
        // Vision icon is updated via OnBushChanged event
    }

    public override void Cleanup()
    {
        if (vision != null)
        {
            vision.OnBushChanged -= OnBushChanged;
        }
        base.Cleanup();
    }
}
