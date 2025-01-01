using System.Collections.Generic;
using UnityEngine;

public class Vision : MonoBehaviour
{
    private bool isVisible = false;
    private bool isRendered = false;

    [SerializeField] private bool hasATeam = false;
    private Team currentTeam;

    private GameObject currentBush = null;

    [SerializeField] private List<GameObject> objectsToDisable = new List<GameObject>();
    [SerializeField] private List<Renderer> renderersToDisable = new List<Renderer>();

    private HashSet<int> isVisibleInBush = new HashSet<int>();

    public bool IsRendered => isRendered;

    public bool IsVisible { get => isVisible; set => isVisible = value; }
    public bool HasATeam { get => hasATeam; set => hasATeam = value; }
    public Team CurrentTeam { get => currentTeam; set => currentTeam = value; }

    public GameObject CurrentBush => currentBush;
    public bool IsInBush => currentBush != null;

    public event System.Action<bool> OnVisibilityChanged;
    public event System.Action<GameObject> OnBushChanged;

    public HashSet<int> IsVisibleInBush { get => isVisibleInBush; set => isVisibleInBush = value; }

    public void SetVisibility(bool isVisible)
    {
        isRendered = isVisible;

        foreach (GameObject obj in objectsToDisable)
        {
            obj.SetActive(isVisible);
        }

        foreach (Renderer renderer in renderersToDisable)
        {
            renderer.enabled = isVisible;
        }

        OnVisibilityChanged?.Invoke(isVisible);
    }

    public void AddObject(GameObject obj)
    {
        objectsToDisable.Add(obj);
    }

    public void AddRenderer(Renderer renderer)
    {
        renderersToDisable.Add(renderer);
    }

    public void ResetObjects()
    {
        objectsToDisable.Clear();
    }

    public void ResetRenderers()
    {
        renderersToDisable.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Bush"))
        {
            currentBush = other.gameObject;
            OnBushChanged?.Invoke(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Bush"))
        {
            currentBush = null;
            OnBushChanged?.Invoke(null);
        }
    }
}
