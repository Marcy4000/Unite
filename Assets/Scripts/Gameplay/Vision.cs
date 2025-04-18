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

    [SerializeField] private List<GameObject> objectsToDisable = new();
    [SerializeField] private List<Renderer> renderersToDisable = new();

    private HashSet<int> isVisibleInBush = new();

    private bool isVisiblyEligible = true;

    public bool IsRendered => isRendered;
    public bool IsInBush => currentBush != null;

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

    private void OnEnable() => VisionManager.Instance?.RegisterVision(this);
    private void OnDisable() => VisionManager.Instance?.UnregisterVision(this);

    public void SetVisibility(bool isVisible)
    {
        pendingVisibilityState = isVisible;

        if (!hasAppliedInitialVisibility && (renderersToDisable.Count == 0 && objectsToDisable.Count == 0))
            return;

        hasAppliedInitialVisibility = true;

        foreach (GameObject obj in objectsToDisable)
            obj.SetActive(isVisible);

        foreach (Renderer renderer in renderersToDisable)
            renderer.enabled = isVisible;

        isRendered = isVisible;
        OnVisibilityChanged?.Invoke(isVisible);
    }

    public void AddObject(GameObject obj)
    {
        objectsToDisable.Add(obj);
        SetVisibility(pendingVisibilityState);
    }

    public void AddRenderer(Renderer renderer)
    {
        renderersToDisable.Add(renderer);
        SetVisibility(pendingVisibilityState);
    }

    public void ResetObjects() => objectsToDisable.Clear();
    public void ResetRenderers() => renderersToDisable.Clear();

    public void RevealTemporarily(RevealType reason, float duration = 2f)
    {
        temporarilyRevealed = true;
        revealTimer = duration;
    }

    private void Update()
    {
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
        if (other.TryGetComponent(out BushCollider bushCollider))
        {
            var bush = bushCollider.BushRoot;
            if (bush != null && currentBush != bush)
            {
                currentBush = bush;
                OnBushChanged?.Invoke(bush);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out BushCollider bushCollider))
        {
            var bush = bushCollider.BushRoot;
            if (bush != null && currentBush == bush)
            {
                currentBush = null;
                OnBushChanged?.Invoke(null);
            }
        }
    }
}
