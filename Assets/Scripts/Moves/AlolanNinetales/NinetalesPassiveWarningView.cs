using UnityEngine;

/// <summary>A fixed boundary and growing floor fill, made from authored prefab meshes.</summary>
public class NinetalesPassiveWarningView : MonoBehaviour
{
    [SerializeField] private Transform buildup;
    private float began, duration = 1;
    public float Progress => Mathf.Clamp01((Time.time - began) / duration);

    public void Initialize(float elapsed, float lifetime, float radius)
    {
        duration = Mathf.Max(0.01f, lifetime); began = Time.time - Mathf.Max(0, elapsed);
        transform.localScale = new Vector3(radius, 1, radius);
        Refresh();
    }
    public void Refresh()
    {
        float progress = Progress;
        if (buildup != null) buildup.localScale = new Vector3(progress, 1, progress);
        if (progress >= 1) Destroy(gameObject);
    }
    private void Update() { Refresh(); }
}
