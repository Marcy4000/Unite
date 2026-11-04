using System.Collections.Generic;
using UnityEngine;

public enum RevealType
{
    Attack,
    DamageTaken,
    AbilityCast,
    RevealedBySkill,
    TrueSight
}

public class Vision : MonoBehaviour
{
    private bool isRendered = false;

    [SerializeField] private bool hasATeam = false;
    private Team currentTeam;
    private GameObject currentBush = null;
    private readonly Dictionary<Collider, GameObject> bushOverlaps = new();
    private readonly HashSet<GameObject> occupiedBushes = new();
    private readonly List<Collider> staleBushOverlaps = new();
    private Collider bodyCollider;
    private Pokemon owner;
    private bool needsBushRefresh;
    private Collider[] bushQueryResults = new Collider[16];

    [SerializeField] private List<GameObject> objectsToDisable = new();
    [SerializeField] private List<Renderer> renderersToDisable = new();

    private HashSet<int> isVisibleInBush = new();

    private bool isVisiblyEligible = true;

    public bool IsRendered => isRendered;
    public bool IsInBush => currentBush != null;
    public bool IsRevealedByBushCombat => owner != null && owner.IsRevealedByBushCombat;

    public bool HasATeam { get => hasATeam; set => hasATeam = value; }
    public Team CurrentTeam { get => currentTeam; set => currentTeam = value; }
    public GameObject CurrentBush => currentBush;
    public HashSet<int> IsVisibleInBush => isVisibleInBush;

    private bool pendingVisibilityState = false;
    private bool hasAppliedInitialVisibility = false;

    private float revealTimer = 0f;
    private bool temporarilyRevealed = false;

    public bool IsVisiblyEligible
    {
        get => isVisiblyEligible;
        set
        {
            isVisiblyEligible = value;
            if (!isVisiblyEligible)
                SetVisibility(false);
        }
    }

    public bool TemporarilyRevealed => temporarilyRevealed;

    public event System.Action<bool> OnVisibilityChanged;
    public event System.Action<GameObject> OnBushChanged;

    private void Awake()
    {
        bodyCollider = GetComponent<Collider>();
        owner = GetComponent<Pokemon>();
    }

    private void OnEnable()
    {
        VisionManager.Instance?.RegisterVision(this);
        needsBushRefresh = true;
    }
    private void OnDisable()
    {
        VisionManager.Instance?.UnregisterVision(this);
        ClearTransientState();
    }

    public void SetVisibility(bool isVisible)
    {
        pendingVisibilityState = isVisible;

        if (!hasAppliedInitialVisibility && (renderersToDisable.Count == 0 && objectsToDisable.Count == 0))
            return;

        if (hasAppliedInitialVisibility && isRendered == isVisible) return;
        ApplyVisibility(isVisible);
    }

    private void ApplyVisibility(bool isVisible)
    {
        bool changed = !hasAppliedInitialVisibility || isRendered != isVisible;
        hasAppliedInitialVisibility = true;

        foreach (GameObject obj in objectsToDisable)
            if (obj != null && obj.activeSelf != isVisible) obj.SetActive(isVisible);

        foreach (Renderer renderer in renderersToDisable)
            if (renderer != null) renderer.enabled = isVisible;

        isRendered = isVisible;
        if (changed) OnVisibilityChanged?.Invoke(isVisible);
    }

    public void AddObject(GameObject obj)
    {
        if (obj == null || objectsToDisable.Contains(obj)) return;
        objectsToDisable.Add(obj);
        ApplyVisibility(pendingVisibilityState);
    }

    public void AddRenderer(Renderer renderer)
    {
        if (renderer == null || renderersToDisable.Contains(renderer)) return;
        renderersToDisable.Add(renderer);
        ApplyVisibility(pendingVisibilityState);
    }

    public void ResetObjects() => objectsToDisable.Clear();
    public void ResetRenderers() => renderersToDisable.Clear();

    public void ClearTransientState()
    {
        temporarilyRevealed = false;
        revealTimer = 0;
        isVisibleInBush.Clear();
        bushOverlaps.Clear();
        occupiedBushes.Clear();
        if (currentBush != null)
        {
            currentBush = null;
            OnBushChanged?.Invoke(null);
        }
    }

    public void RevealTemporarily(RevealType reason, float duration = 2f)
    {
        temporarilyRevealed = true;
        revealTimer = duration;
    }

    private void Update()
    {
        if (needsBushRefresh) RefreshBushOverlaps();
        PruneBushOverlaps();
        if (temporarilyRevealed)
        {
            revealTimer -= Time.deltaTime;
            if (revealTimer <= 0f)
            {
                temporarilyRevealed = false;
                // Reevaluate visibility naturally (e.g., VisionController will update on next tick)
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (TrackBushOverlap(other)) UpdateBushMembership();
    }

    // Rebuild after a reset while still inside grass, even without a fresh enter.
    private void OnTriggerStay(Collider other) => OnTriggerEnter(other);

    private void OnTriggerExit(Collider other)
    {
        // A child vision/ability trigger may exit before the body does.
        if (OverlapsBody(other)) return;
        if (bushOverlaps.Remove(other)) UpdateBushMembership();
    }

    public bool OccupiesBush(GameObject bush) => bush != null && occupiedBushes.Contains(bush);

    public bool SharesBushWith(Vision other) => other != null && occupiedBushes.Overlaps(other.occupiedBushes);

    private bool OverlapsBody(Collider other)
    {
        if (bodyCollider == null) bodyCollider = GetComponent<Collider>();
        return bodyCollider != null && bodyCollider.enabled && other != null && other.enabled
            && other.gameObject.activeInHierarchy
            && Physics.ComputePenetration(bodyCollider, bodyCollider.transform.position, bodyCollider.transform.rotation,
                other, other.transform.position, other.transform.rotation, out _, out _);
    }

    private bool TrackBushOverlap(Collider other)
    {
        if (!CanTrackBushes || !other.TryGetComponent(out BushCollider bush)
            || !bush.isActiveAndEnabled || bush.BushRoot == null || !OverlapsBody(other)) return false;
        if (bushOverlaps.TryGetValue(other, out GameObject root) && root == bush.BushRoot) return false;
        bushOverlaps[other] = bush.BushRoot;
        return true;
    }

    private void PruneBushOverlaps()
    {
        staleBushOverlaps.Clear();
        foreach (var overlap in bushOverlaps)
            if (overlap.Value == null || !OverlapsBody(overlap.Key)
                || !overlap.Key.TryGetComponent(out BushCollider bush) || !bush.isActiveAndEnabled)
                staleBushOverlaps.Add(overlap.Key);
        if (staleBushOverlaps.Count == 0) return;
        foreach (Collider other in staleBushOverlaps) bushOverlaps.Remove(other);
        UpdateBushMembership();
    }

    // Invisibility affects being seen, not the invisible player's own bush vision.
    private bool CanTrackBushes => isActiveAndEnabled && (owner == null || owner.CurrentHp > 0);

    private void UpdateBushMembership()
    {
        if (occupiedBushes.SetEquals(bushOverlaps.Values)) return;
        occupiedBushes.Clear();
        foreach (GameObject root in bushOverlaps.Values)
            if (root != null) occupiedBushes.Add(root);

        // Keep the representative stable in overlaps. Visibility compares the
        // full root sets, never whichever collider happened to fire last.
        if (!occupiedBushes.Contains(currentBush))
        {
            currentBush = null;
            foreach (GameObject root in occupiedBushes)
                if (currentBush == null || root.GetInstanceID() < currentBush.GetInstanceID()) currentBush = root;
        }
        OnBushChanged?.Invoke(currentBush);
    }

    // Teleports do not guarantee trigger exits/enters. Refresh the destination
    // immediately without resetting the Pokemon's independent combat deadline.
    public void RefreshBushOverlaps()
    {
        needsBushRefresh = false;
        bushOverlaps.Clear();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider>();
        if (CanTrackBushes && bodyCollider != null && bodyCollider.enabled)
        {
            Bounds bounds = bodyCollider.bounds;
            int count;
            while (true)
            {
                count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, bushQueryResults,
                    Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
                if (count < bushQueryResults.Length) break;
                System.Array.Resize(ref bushQueryResults, bushQueryResults.Length * 2);
            }
            for (int i = 0; i < count; i++) TrackBushOverlap(bushQueryResults[i]);
            System.Array.Clear(bushQueryResults, 0, bushQueryResults.Length);
        }
        UpdateBushMembership();
    }
}
