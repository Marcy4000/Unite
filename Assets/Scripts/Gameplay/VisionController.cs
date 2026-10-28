using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class VisionController : MonoBehaviour
{
    [SerializeField] private float visionRange = 10f;

    private readonly HashSet<Vision> visibleObjects = new();

    private SphereCollider visionCollider;
    private Pokemon owner;
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
        owner = GetComponentInParent<Pokemon>();
    }

    private void OnEnable() => VisionManager.Instance?.RegisterController(this);
    private void OnDisable()
    {
        VisionManager.Instance?.UnregisterController(this);
        ClearTrackedVisions();
    }

    // Trigger exits are not guaranteed when a CharacterController is disabled
    // for a teleport. Never let a stale overlap reveal a respawned enemy forever.
    public bool ContainsVision(Vision vision)
    {
        if (vision == null || !isActiveAndEnabled || !IsEnabled || IsBlinded ||
            (owner != null && owner.CurrentHp <= 0) || visionCollider == null || !visionCollider.enabled) return false;
        Vector3 scale = transform.lossyScale;
        float radius = visionCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        return (vision.transform.position - transform.TransformPoint(visionCollider.center)).sqrMagnitude <= radius * radius;
    }

    public bool CanSee(Vision vision) => ContainsVision(vision) && (!vision.IsInBush || vision.CurrentBush == CurrentBush);

    public void ClearTrackedVisions()
    {
        foreach (var vision in visibleObjects)
        {
            if (vision == null) continue;
            vision.OnBushChanged -= OnVisionBushChanged;
            vision.IsVisibleInBush.Remove(gameObject.GetInstanceID());
        }
        visibleObjects.Clear();
        currentBush = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out Vision vision)) return;

        if (!visibleObjects.Add(vision)) return;
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
        visibleObjects.RemoveWhere(vision => vision == null);
        foreach (var vision in visibleObjects)
        {
            if (vision == null) continue;
            if (vision.CurrentBush == currentBush)
                vision.IsVisibleInBush.Add(gameObject.GetInstanceID());
            else
                vision.IsVisibleInBush.Remove(gameObject.GetInstanceID());
        }
    }
}
