using System.Collections.Generic;
using UnityEngine;

// Physics overlaps are broad-phase candidates, not proof that a character is still there.
// Validate against authored shape data transformed by the CURRENT transform, never cached
// Collider.bounds/ClosestPoint: those can retain a pre-teleport pose until the next physics sync.
public static class CombatTargeting
{
    public static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    public static bool WithinMeleeRange(Vector3 origin, Vector3 target, float range) => Finite(origin) && Finite(target)
        && float.IsFinite(range) && range > 0 && (target - origin).sqrMagnitude <= (range + 0.5f) * (range + 0.5f);

    public static bool TryGetPokemon(Collider collider, out Pokemon pokemon)
    {
        pokemon = null;
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy
            || collider.GetComponentInParent<VisionController>() != null) return false;
        pokemon = collider.GetComponentInParent<Pokemon>();
        return pokemon != null;
    }

    public static HashSet<Pokemon> SphereTargets(Vector3 center, float radius)
    {
        var result = new HashSet<Pokemon>();
        if (!Finite(center) || !float.IsFinite(radius) || radius < 0) return result;
        foreach (Collider collider in Physics.OverlapSphere(center, radius))
            if (TryGetPokemon(collider, out var pokemon) && OverlapsSphereNow(collider, center, radius)) result.Add(pokemon);
        return result;
    }

    public static HashSet<Pokemon> BoxTargets(Vector3 center, Vector3 halfExtents, Quaternion rotation)
    {
        var result = new HashSet<Pokemon>();
        if (!Finite(center) || !Finite(halfExtents) || halfExtents.x < 0 || halfExtents.y < 0 || halfExtents.z < 0) return result;
        foreach (Collider collider in Physics.OverlapBox(center, halfExtents, rotation))
            if (TryGetPokemon(collider, out var pokemon) && InCurrentBoxBounds(collider, center, halfExtents, rotation)) result.Add(pokemon);
        return result;
    }

    public static bool OverlapsSphereNow(Collider collider, Vector3 center, float radius)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy
            || !Finite(center) || !float.IsFinite(radius) || radius < 0) return false;
        Transform transform = collider.transform;
        if (!Finite(transform.position) || !Finite(transform.lossyScale)) return false;
        Vector3 scale = Abs(transform.lossyScale);
        if (collider is SphereCollider sphere)
            return Touches(center, transform.TransformPoint(sphere.center), radius + sphere.radius * Max(scale));
        if (collider is CharacterController controller)
            return CapsuleTouches(center, radius, transform, controller.center, controller.height, controller.radius, 1, scale);
        if (collider is CapsuleCollider capsule)
            return CapsuleTouches(center, radius, transform, capsule.center, capsule.height, capsule.radius, capsule.direction, scale);
        if (collider is BoxCollider box)
            return BoxTouches(center, radius, transform, box.center, box.size * 0.5f);
        if (collider is MeshCollider mesh && mesh.sharedMesh != null)
            return BoxTouches(center, radius, transform, mesh.sharedMesh.bounds.center, mesh.sharedMesh.bounds.extents);
        return false;
    }

    // Conservative rejection only: the original OverlapBox still supplies narrow-phase
    // contacts. Bounding spheres here must not clip valid rotated/large hurtboxes.
    public static bool InCurrentBoxBounds(Collider collider, Vector3 center, Vector3 halfExtents, Quaternion rotation)
    {
        if (!Finite(center) || !Finite(halfExtents) || !CurrentBounds(collider, out Vector3 bodyCenter, out float bodyRadius)) return false;
        Vector3 local = Quaternion.Inverse(rotation) * (bodyCenter - center);
        Vector3 nearest = Clamp(local, halfExtents);
        return (local - nearest).sqrMagnitude <= bodyRadius * bodyRadius + 0.0001f;
    }

    private static bool CurrentBounds(Collider collider, out Vector3 center, out float radius)
    {
        center = default; radius = 0;
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) return false;
        Transform transform = collider.transform;
        if (!Finite(transform.position) || !Finite(transform.lossyScale)) return false;
        Vector3 scale = Abs(transform.lossyScale);
        if (collider is SphereCollider sphere) { center = transform.TransformPoint(sphere.center); radius = sphere.radius * Max(scale); }
        else if (collider is CharacterController controller)
        { center = transform.TransformPoint(controller.center); radius = Mathf.Max(controller.height * scale.y * 0.5f, controller.radius * Mathf.Max(scale.x, scale.z)); }
        else if (collider is CapsuleCollider capsule)
        { center = transform.TransformPoint(capsule.center); radius = Mathf.Max(capsule.height * scale[capsule.direction] * 0.5f, capsule.radius * RadialScale(scale, capsule.direction)); }
        else if (collider is BoxCollider box) { center = transform.TransformPoint(box.center); radius = Vector3.Scale(box.size * 0.5f, scale).magnitude; }
        else if (collider is MeshCollider mesh && mesh.sharedMesh != null)
        { center = transform.TransformPoint(mesh.sharedMesh.bounds.center); radius = Vector3.Scale(mesh.sharedMesh.bounds.extents, scale).magnitude; }
        else return false;
        return Finite(center) && float.IsFinite(radius);
    }
    private static bool CapsuleTouches(Vector3 query, float radius, Transform transform, Vector3 localCenter, float height, float bodyRadius, int axis, Vector3 scale)
    {
        Vector3 center = transform.TransformPoint(localCenter);
        float r = bodyRadius * RadialScale(scale, axis);
        float halfSegment = Mathf.Max(0, height * scale[axis] * 0.5f - r);
        Vector3 localAxis = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
        Vector3 direction = transform.TransformDirection(localAxis).normalized;
        Vector3 closest = center + direction * Mathf.Clamp(Vector3.Dot(query - center, direction), -halfSegment, halfSegment);
        return Touches(query, closest, radius + r);
    }
    private static bool BoxTouches(Vector3 query, float radius, Transform transform, Vector3 center, Vector3 half)
    {
        Vector3 local = transform.InverseTransformPoint(query) - center;
        return Touches(query, transform.TransformPoint(center + Clamp(local, half)), radius);
    }
    private static bool Touches(Vector3 first, Vector3 second, float radius) => Finite(second) && (first - second).sqrMagnitude <= radius * radius + 0.0001f;
    private static Vector3 Clamp(Vector3 value, Vector3 half) => Vector3.Min(Vector3.Max(value, -half), half);
    private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    private static float Max(Vector3 value) => Mathf.Max(value.x, Mathf.Max(value.y, value.z));
    private static float RadialScale(Vector3 scale, int axis) => axis == 0 ? Mathf.Max(scale.y, scale.z) : axis == 1 ? Mathf.Max(scale.x, scale.z) : Mathf.Max(scale.x, scale.y);
}
