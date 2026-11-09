using UnityEngine;

/// <summary>Follow a replicated mark; hide with its victim and retire promptly on cleanse.</summary>
public class UmbreonMarkVisual : MonoBehaviour
{
    private Pokemon target;
    private ushort id;
    private float grace;
    private Renderer[] renderers;
    public void Initialize(Pokemon victim, ushort mark)
    {
        target = victim; id = mark; grace = Time.time + 0.25f;
        renderers = GetComponentsInChildren<Renderer>(true);
    }
    private void LateUpdate()
    {
        if (target == null || !target.IsSpawned || target.CurrentHp <= 0 || (Time.time >= grace && !target.HasStatusEffect(id)))
        { Destroy(gameObject); return; }
        transform.position = target.transform.position + Vector3.up * 1.7f;
        bool visible = !target.TryGetComponent(out Vision vision) || vision.IsRendered;
        if (renderers != null) foreach (var renderer in renderers) if (renderer != null) renderer.enabled = visible;
    }
}
