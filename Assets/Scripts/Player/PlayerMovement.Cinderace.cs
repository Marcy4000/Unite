using UnityEngine;

public partial class PlayerMovement
{
    // Casts own a movement restriction, so ordinary StartDash (which is gated
    // by CanMove) cannot drive them. Explicit steps cross walls while still
    // validating the no-walls walkmesh and never leave a global traversal flag.
    public Vector3 CinderaceDashStep(Vector3 displacement)
    {
        Vector3 start = transform.position;
        Collider previous = activeWalkmesh;
        try
        {
            activeWalkmesh = walkableNoWalls;
            Vector3 destination = ClampMovementToWalkmesh(start, start + displacement);
            characterController.Move(destination - start);
        }
        finally { activeWalkmesh = previous; }
        return transform.position - start;
    }

    public void FinishCinderaceTraversal()
    {
        if (characterController == null || !characterController.enabled) return;
        if (!IsPositionValidOnWalkmesh(transform.position))
            characterController.Move(GetClosestValidPosition(transform.position) - transform.position);
    }
}
