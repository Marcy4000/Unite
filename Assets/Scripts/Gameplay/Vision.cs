using System.Collections.Generic;
using UnityEngine;

public class Vision : MonoBehaviour
{
    private bool isVisible = false;
    private bool isRendered = false;

    [SerializeField] private bool hasATeam = false;
    private bool currentTeam = false;

    [SerializeField] private List<GameObject> objectsToDisable = new List<GameObject>();
    [SerializeField] private List<Renderer> renderersToDisable = new List<Renderer>();

    public bool IsRendered => isRendered;

    public bool IsVisible { get => isVisible; set => isVisible = value; }
    public bool HasATeam { get => hasATeam; set => hasATeam = value; }
    public bool CurrentTeam { get => currentTeam; set => currentTeam = value; }

    public event System.Action<bool> onVisibilityChanged;

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

        onVisibilityChanged?.Invoke(isVisible);
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
}
