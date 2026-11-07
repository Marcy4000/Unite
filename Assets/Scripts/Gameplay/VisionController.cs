using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public class VisionController : MonoBehaviour
{
    [SerializeField] private float visionRange = 10f;

    private readonly HashSet<Vision> visibleObjects = new();

    private SphereCollider visionCollider;
    private Pokemon owner;
    private Vision ownerVision;
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
        ownerVision = GetComponentInParent<Vision>();
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
        return vision != null && TryGetVisibilitySnapshot(out var snapshot)
            && snapshot.Contains(vision.transform.position);
    }

    // Scoped to one manager pass. Direct queries always capture fresh geometry,
    // including moves/teleports or collider changes within the same frame.
    internal bool TryGetVisibilitySnapshot(out VisibilitySnapshot snapshot)
    {
        snapshot = default;
        if (!isActiveAndEnabled || !IsEnabled || IsBlinded ||
            (owner != null && owner.CurrentHp <= 0) || visionCollider == null || !visionCollider.enabled) return false;
        Transform controllerTransform = transform;
        Vector3 scale = controllerTransform.lossyScale;
        float radius = visionCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        snapshot = new VisibilitySnapshot(this, controllerTransform.TransformPoint(visionCollider.center), radius * radius);
        return true;
    }

    internal readonly struct VisibilitySnapshot
    {
        private readonly VisionController controller;
        private readonly Vector3 center;
        private readonly float radiusSquared;

        internal VisibilitySnapshot(VisionController controller, Vector3 center, float radiusSquared)
        {
            this.controller = controller;
            this.center = center;
            this.radiusSquared = radiusSquared;
        }

        internal bool Contains(Vector3 position) => (position - center).sqrMagnitude <= radiusSquared;

        // Target eligibility is checked once by the manager before scanning controllers.
        internal bool CanSeeEligible(Vision vision, Vector3 position) => controller != null
            && Contains(position) && controller.CanSeeBush(vision);
    }

    private bool SharesBush(Vision vision) => ownerVision != null
        ? vision.SharesBushWith(ownerVision) : vision.OccupiesBush(currentBush);

    public bool CanSee(Vision vision) => ContainsVision(vision)
        && vision.isActiveAndEnabled && vision.IsVisiblyEligible
        && CanSeeBush(vision);

    private bool CanSeeBush(Vision vision) => !vision.IsInBush || vision.IsRevealedByBushCombat || SharesBush(vision);

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
            if (SharesBush(vision))
                vision.IsVisibleInBush.Add(gameObject.GetInstanceID());
            else
                vision.IsVisibleInBush.Remove(gameObject.GetInstanceID());
        }
    }
}
