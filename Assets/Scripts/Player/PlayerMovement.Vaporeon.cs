using System.Collections;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerMovement
{
    private bool vaporeonDiveTraversal, vaporeonDiveHasSafe;
    private Vector3 vaporeonDiveLastSafe;
    public bool IsVaporeonDiveTraversing => vaporeonDiveTraversal;

    public void BeginVaporeonDiveTraversal()
    {
        if (vaporeonDiveTraversal || walkableWithWalls == null || walkableNoWalls == null) return;
        // Do not enable unbounded movement when a map's validation data is absent.
        vaporeonDiveHasSafe = false;
        if (!TryVaporeonDiveExit(transform.position, out var safe)) return;
        vaporeonDiveLastSafe = safe; vaporeonDiveHasSafe = true;
        vaporeonDiveTraversal = true; currentMovement.y = 0;
        UpdateActiveWalkmesh();
    }
    public void TrackVaporeonDivePosition()
    {
        if (!vaporeonDiveTraversal || !IsVaporeonDiveLandingValid(transform.position)) return;
        vaporeonDiveLastSafe = transform.position; vaporeonDiveHasSafe = true;
    }
    public Vector3 MoveVaporeonDive(Vector3 displacement)
    {
        if (!vaporeonDiveTraversal || !VaporMoveRules.Finite(displacement)) return Vector3.zero;
        Vector3 start = transform.position; displacement.y = 0;
        int steps = Mathf.Clamp(Mathf.CeilToInt(displacement.magnitude / 0.1f), 1, 512);
        for (int i = 1; i <= steps; i++)
        {
            Vector3 point = start + displacement * (i / (float)steps);
            if (!VaporeonWalkmeshContains(walkableNoWalls, -40, point)) break;
            // Same approach as the wall-crossing Marshadow dashes: direct
            // movement bypasses physical colliders as well as walkmesh walls.
            transform.position = point;
            TrackVaporeonDivePosition();
        }
        return transform.position - start;
    }
    public bool IsVaporeonDiveLandingValid(Vector3 point)
    {
        if (!VaporMoveRules.Finite(point) || walkableWithWalls == null || walkableNoWalls == null) return false;
        bool Valid(Vector3 p) => VaporeonWalkmeshContains(walkableWithWalls, -30, p) && VaporeonWalkmeshContains(walkableNoWalls, -40, p);
        if (!Valid(point)) return false;
        // Leave room for the character, not just its center, when emerging.
        float radius = characterController != null ? (characterController.radius + characterController.skinWidth)
            * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) : 0.5f;
        return Valid(point + Vector3.right * radius) && Valid(point - Vector3.right * radius)
            && Valid(point + Vector3.forward * radius) && Valid(point - Vector3.forward * radius);
    }
    private static bool VaporeonWalkmeshContains(Collider mesh, float height, Vector3 point)
    {
        return mesh != null && mesh.Raycast(new Ray(new Vector3(point.x, height + 1, point.z), Vector3.down), out _, 2);
    }
    public bool TryVaporeonDiveExit(Vector3 requested, out Vector3 destination)
    {
        destination = requested;
        if (!VaporMoveRules.Finite(requested)) return false;
        if (IsVaporeonDiveLandingValid(requested)) return true;
        // Prefer a nearby edge of terrain; a thick wall falls back to the last
        // grounded position tracked during this particular Dive, not its origin.
        for (int ring = 1; ring <= 8; ring++)
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12;
                Vector3 candidate = requested + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (ring * 0.25f);
                if (IsVaporeonDiveLandingValid(candidate)) { destination = candidate; return true; }
            }
        if (vaporeonDiveHasSafe && IsVaporeonDiveLandingValid(vaporeonDiveLastSafe)) { destination = vaporeonDiveLastSafe; return true; }
        return false;
    }
    public Vector3 EndVaporeonDiveTraversal(bool reposition = true)
    {
        if (!vaporeonDiveTraversal) return transform.position;
        if (reposition && TryVaporeonDiveExit(transform.position, out var exit)) transform.position = exit;
        vaporeonDiveTraversal = false; vaporeonDiveHasSafe = false; currentMovement.y = 0;
        UpdateActiveWalkmesh();
        return transform.position;
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void VaporeonDiveExitRPC(Vector3 exit)
    {
        bool valid = IsVaporeonDiveLandingValid(exit);
        EndVaporeonDiveTraversal(!valid);
        if (pokemon != null && pokemon.CurrentHp > 0 && valid) transform.position = exit;
    }

    // Rain crosses walls; Whirlpool pulls and Dive's shove still respect them.
    // None changes IsFlying, controller.enabled, or another move's dash state.
    public Vector3 VaporeonHopDestination(Vector3 requested)
    {
        Vector3 start = transform.position;
        requested = VaporMoveRules.ClampDestination(start, requested, 4f);
        return GetWallCrossingDestination(requested);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void VaporeonDisplaceRPC(Vector3 center, float distance, bool inward)
    {
        if (!VaporMoveRules.Finite(center) || pokemon.CurrentHp <= 0 || !CanBeKnockedBack
            || pokemon.HasStatusEffect(StatusType.Unstoppable) || pokemon.HasStatusEffect(StatusType.Invincible)
            || pokemon.HasStatusEffect(StatusType.HindranceResistance)) return;
        Vector3 offset = (inward ? center - transform.position : transform.position - center); offset.y = 0;
        float length = Mathf.Min(Mathf.Clamp(distance, 0, VaporMoveRules.PullDistance), inward ? offset.magnitude : distance);
        if (length < 0.01f || offset.sqrMagnitude < 0.001f) return;
        StartCoroutine(VaporeonDisplacement(offset.normalized, length));
    }
    private IEnumerator VaporeonDisplacement(Vector3 direction, float remaining)
    {
        // Explicit displacement works during a stun; the shared knockback queue does not.
        while (remaining > 0.01f && pokemon.CurrentHp > 0 && !pokemon.HasStatusEffect(StatusType.Unstoppable)
            && !pokemon.HasStatusEffect(StatusType.Invincible))
        {
            float step = Mathf.Min(remaining, 8f * Time.deltaTime);
            float moved = Vector3.Dot(MoveWithWalls(direction * step), direction);
            if (moved < step * 0.2f) yield break;
            remaining -= Mathf.Max(0, moved); yield return null;
        }
    }
}
