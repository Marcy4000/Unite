using UnityEngine;

public class TrevenantMarkVisual : MonoBehaviour
{
    private Pokemon target;
    private ushort id;
    private float began;
    private Renderer[] renderers;
    public void Initialize(Pokemon pokemon, ushort markId) { target = pokemon; id = markId; began = Time.time; renderers = GetComponentsInChildren<Renderer>(true); }
    private void LateUpdate()
    {
        if (target == null || !target.IsSpawned || target.CurrentHp <= 0 || (Time.time - began > 0.25f && !target.HasStatusEffect(id)))
        { Destroy(gameObject); return; }
        transform.position = target.transform.position + Vector3.up * 2;
        bool visible = !target.TryGetComponent(out Vision vision) || vision.IsRendered;
        if (renderers != null) foreach (var renderer in renderers) if (renderer != null) renderer.enabled = visible;
        transform.Rotate(Vector3.up, 60 * Time.deltaTime);
    }
}
