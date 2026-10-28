using Unity.Netcode;
using UnityEngine;

// Pickup/objective placement must not snap onto actor hitboxes, goal models or
// triggers just because those happen to share the terrain's layer.
public static class GroundPlacement
{
    private static RaycastHit[] hits = new RaycastHit[32];

    public static bool TryGetGround(Vector3 origin, int layerMask, out Vector3 point)
    {
        point = origin;
        if (!float.IsFinite(origin.x) || !float.IsFinite(origin.y) || !float.IsFinite(origin.z)) return false;
        int count;
        var ray = new Ray(origin + Vector3.up * 5, Vector3.down);
        while (true)
        {
            count = Physics.RaycastNonAlloc(ray, hits, 25, layerMask, QueryTriggerInteraction.Ignore);
            if (count < hits.Length || hits.Length >= 512) break;
            hits = new RaycastHit[hits.Length * 2];
        }
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            var collider = hit.collider;
            if (collider == null || hit.normal.y < 0.5f || hit.distance >= nearest ||
                collider.attachedRigidbody != null || collider.CompareTag("Wall") ||
                collider.GetComponentInParent<NetworkObject>() != null ||
                collider.GetComponentInParent<GoalZone>() != null || collider.GetComponentInParent<Pokemon>() != null) continue;
            nearest = hit.distance;
            point = hit.point;
        }
        System.Array.Clear(hits, 0, count);
        return !float.IsPositiveInfinity(nearest);
    }
}
