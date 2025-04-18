using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class VisionController : MonoBehaviour
{
    [SerializeField] private float visionRange = 10f;

    private readonly HashSet<Vision> visibleObjects = new();

    private SphereCollider visionCollider;
    private GameObject currentBush;

    public Team CurrentTeam { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsBlinded { get; set; } = false;
    public GameObject CurrentBush
    {
        get => currentBush;
        set
        {
            currentBush = value;
            UpdateBushVision();
        }
    }

    private void Awake()
    {
        visionCollider = GetComponent<SphereCollider>();
        visionCollider.radius = visionRange;
        visionCollider.isTrigger = true;
    }

    private void OnEnable() => VisionManager.Instance?.RegisterController(this);
    private void OnDisable() => VisionManager.Instance?.UnregisterController(this);

    public bool ContainsVision(Vision vision) => visibleObjects.Contains(vision);

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out Vision vision)) return;

        visibleObjects.Add(vision);
        vision.OnBushChanged += OnVisionBushChanged;
        UpdateBushVision();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out Vision vision)) return;

        visibleObjects.Remove(vision);
        vision.OnBushChanged -= OnVisionBushChanged;

        if (vision.IsVisibleInBush.Contains(gameObject.GetInstanceID()))
            vision.IsVisibleInBush.Remove(gameObject.GetInstanceID());
    }

    private void OnVisionBushChanged(GameObject newBush)
    {
        UpdateBushVision();
    }

    private void UpdateBushVision()
    {
        foreach (var vision in visibleObjects)
        {
            if (vision.CurrentBush == currentBush)
                vision.IsVisibleInBush.Add(gameObject.GetInstanceID());
            else
                vision.IsVisibleInBush.Remove(gameObject.GetInstanceID());
        }
    }
}
