using UnityEngine;

/// <summary>Authored bones/aura follow the imported animator, never a procedural match-time mesh.</summary>
public class LucarioAttachedVisual : MonoBehaviour
{
    private PlayerManager actor;
    private Transform anchor;
    public void Bind(PlayerManager owner, string bone)
    {
        actor = owner; anchor = owner.transform;
        if (!string.IsNullOrEmpty(bone))
            foreach (var t in owner.GetComponentsInChildren<Transform>(true)) if (t.name == bone) { anchor = t; break; }
        LateUpdate();
    }
    private void LateUpdate()
    {
        if (actor == null || anchor == null || actor.Pokemon.CurrentHp <= 0) { Destroy(gameObject); return; }
        transform.SetPositionAndRotation(anchor.position, anchor.rotation);
    }
}
