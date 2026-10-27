using UnityEngine;

/// <summary>Local presentation of a baked prefab; no runtime mesh construction.</summary>
public class TimedCombatVisual : MonoBehaviour
{
    private float began, duration = 0.3f;
    private Vector3 size;
    [SerializeField] private float growth = 0.25f;
    public void Initialize(float lifetime, Vector3 scale)
    {
        began = Time.time; duration = Mathf.Max(0.01f, lifetime); size = scale;
        transform.localScale = scale * (1 - growth);
    }
    private void Update()
    {
        float progress = Mathf.Clamp01((Time.time - began) / duration);
        transform.localScale = size * Mathf.Lerp(1 - growth, 1, progress);
        if (progress >= 1) Destroy(gameObject);
    }
    public static GameObject Spawn(string resource, Vector3 position, Vector3 direction, Vector3 scale, float lifetime)
    {
        var prefab = Resources.Load<GameObject>(resource);
        if (prefab == null) return null;
        var instance = Instantiate(prefab, position, direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction) : Quaternion.identity);
        var visual = instance.GetComponent<TimedCombatVisual>();
        if (visual != null) visual.Initialize(lifetime, scale);
        else Destroy(instance, lifetime);
        return instance;
    }
}
