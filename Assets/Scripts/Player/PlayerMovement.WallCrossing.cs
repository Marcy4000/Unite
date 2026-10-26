using UnityEngine;

public partial class PlayerMovement
{
    private static bool WallCrossingFinite(Vector3 point) => float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);

    private static bool WallCrossingContains(Collider mesh, float height, Vector3 point)
    {
        return mesh != null && mesh.Raycast(new Ray(new Vector3(point.x, height + 1, point.z), Vector3.down), out _, 2);
    }

    public bool IsWallCrossingLandingValid(Vector3 point)
    {
        if (!WallCrossingFinite(point)) return false;
        bool Valid(Vector3 p) => WallCrossingContains(walkableWithWalls, -30, p) && WallCrossingContains(walkableNoWalls, -40, p);
        float radius = characterController != null ? (characterController.radius + characterController.skinWidth)
            * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) : 0.5f;
        return Valid(point) && Valid(point + Vector3.right * radius) && Valid(point - Vector3.right * radius)
            && Valid(point + Vector3.forward * radius) && Valid(point - Vector3.forward * radius);
    }

    // Search past internal walls, but never across gaps in the map boundary or
    // beyond the requested range. Ground-targeted hops use this before casting
    // so their landing effects and owner movement agree on the destination.
    public Vector3 GetWallCrossingDestination(Vector3 requested)
    {
        Vector3 start = transform.position, safe = start;
        if (!WallCrossingFinite(requested)) return safe;
        requested.y = start.y;
        int steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(start, requested) / 0.1f), 1, 512);
        for (int i = 1; i <= steps; i++)
        {
            Vector3 point = Vector3.Lerp(start, requested, i / (float)steps);
            if (!WallCrossingContains(walkableNoWalls, -40, point)) break;
            if (IsWallCrossingLandingValid(point)) safe = point;
        }
        return safe;
    }

    internal Vector3 MoveWallCrossingStep(Vector3 displacement, ref Vector3 safe)
    {
        Vector3 start = transform.position;
        if (!WallCrossingFinite(displacement) || walkableWithWalls == null || walkableNoWalls == null) return Vector3.zero;
        displacement.y = 0;
        int steps = Mathf.Clamp(Mathf.CeilToInt(displacement.magnitude / 0.1f), 1, 512);
        for (int i = 1; i <= steps; i++)
        {
            Vector3 point = start + displacement * (i / (float)steps);
            if (!WallCrossingContains(walkableNoWalls, -40, point)) break;
            // Like Marshadow's dashes, bypass physical colliders too. The cast
            // already owns a movement restriction; no shared dash/flight flags
            // or CharacterController state need to be changed.
            transform.position = point;
            if (IsWallCrossingLandingValid(point)) safe = point;
        }
        return transform.position - start;
    }
}

// A cast-owned traversal, including homing moves whose direction changes each
// frame. End must run before releasing action locks or reporting a landing.
public sealed class WallCrossingTraversal
{
    private PlayerMovement movement;
    private Vector3 lastSafe;

    public void Begin(PlayerMovement owner)
    {
        End();
        movement = owner;
        lastSafe = owner.transform.position;
    }

    public Vector3 Move(Vector3 displacement)
    {
        return movement != null ? movement.MoveWallCrossingStep(displacement, ref lastSafe) : Vector3.zero;
    }

    public void End()
    {
        if (movement == null) return;
        if (!movement.IsWallCrossingLandingValid(movement.transform.position)) movement.transform.position = lastSafe;
        movement = null;
    }
}
