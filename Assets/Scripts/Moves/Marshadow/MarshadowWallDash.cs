using UnityEngine;

// A manually advanced, wall-crossing dash. ActionLock already suspends ordinary
// movement/ground snapping. Direct transform movement also bypasses physical
// wall colliders without disabling the character controller or changing flight.
public sealed class MarshadowWallDash
{
    private PlayerMovement movement;
    private Vector3 start, direction, lastSafePosition;
    private float distance, travelled;
    public bool Finished => movement == null || travelled >= distance - 0.001f;
    public Vector3 Destination => start + direction * distance;

    public void Begin(PlayerMovement owner, Vector3 aim, float range)
    {
        End();
        movement = owner;
        start = lastSafePosition = owner.transform.position;
        direction = MarshadowMoveRules.Direction(aim, owner.transform.forward);
        distance = owner.GetMarshadowDashDistance(direction, range);
        travelled = 0;
    }

    public Vector3 Advance(float requestedDistance)
    {
        if (movement == null) return Vector3.zero;
        Vector3 before = movement.transform.position;
        travelled += Mathf.Clamp(requestedDistance, 0, distance - travelled);
        movement.transform.position = start + direction * travelled;
        if (movement.IsMarshadowDashLandingValid(movement.transform.position)) lastSafePosition = movement.transform.position;
        return movement.transform.position - before;
    }

    public void End()
    {
        if (movement == null) return;
        // Interrupted/dead/replaced casts can stop in the middle of terrain.
        // Return to this dash's last safe point before releasing movement locks.
        if (!movement.IsMarshadowDashLandingValid(movement.transform.position)) movement.transform.position = lastSafePosition;
        movement = null;
    }
}
