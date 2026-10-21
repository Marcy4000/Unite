using System.Collections;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerMovement
{
    private float marshadowWallReportUntil;
    private float marshadowWallStun;
    private ulong marshadowWallAttacker;

    // Use the same no-walls validation surface as StartDash, but leave its
    // coroutine/IsDashing state alone: these moves own their movement timing.
    // Scan through terrain to the last safe landing within range. Stop at map
    // boundaries, not walls, and never extend the dash to find a landing.
    public float GetMarshadowDashDistance(Vector3 direction, float range)
    {
        if (float.IsNaN(range) || float.IsInfinity(range) || range <= 0) return 0;
        direction = MarshadowMoveRules.Direction(direction, transform.forward);
        Vector3 start = transform.position;
        float safeDistance = 0;
        int steps = Mathf.CeilToInt(range / 0.1f);
        for (int i = 1; i <= steps; i++)
        {
            float distance = range * i / steps;
            Vector3 point = start + direction * distance;
            bool inBounds = walkableNoWalls != null ? MarshadowWalkmeshContains(walkableNoWalls, -40f, point)
                : IsMarshadowDashLandingValid(point);
            if (!inBounds) break;
            if (IsMarshadowDashLandingValid(point)) safeDistance = distance;
        }
        return safeDistance;
    }

    public bool IsMarshadowDashLandingValid(Vector3 point)
    {
        return walkableWithWalls == null || MarshadowWalkmeshContains(walkableWithWalls, -30f, point);
    }

    private static bool MarshadowWalkmeshContains(Collider mesh, float height, Vector3 point)
    {
        return mesh.Raycast(new Ray(new Vector3(point.x, height + 1f, point.z), Vector3.down), out _, 2f);
    }

    public void ApplyMarshadowWallKick(Vector3 direction, float stunDuration, ulong attacker)
    {
        if (!IsServer) return;
        marshadowWallReportUntil = Time.time + 1f;
        marshadowWallStun = stunDuration; marshadowWallAttacker = attacker;
        MarshadowWallKickRPC(direction);
    }
    // Test against the walkmesh, never teleport a shadow exit into a wall or off
    // the map. Unlike ordinary movement, exits must use the grounded mesh.
    public bool TryMarshadowExit(Vector3 requested, out Vector3 destination)
    {
        destination = requested;
        if (walkableWithWalls == null) return true;
        if (OnGroundedWalkmesh(requested)) return true;
        for (int ring = 1; ring <= 8; ring++)
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12f;
                Vector3 point = requested + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (ring * 0.25f);
                if (OnGroundedWalkmesh(point)) { destination = point; return true; }
            }
        if (OnGroundedWalkmesh(lastValidPosition)) { destination = lastValidPosition; return true; }
        return false;
    }

    private bool OnGroundedWalkmesh(Vector3 position)
    {
        return Physics.Raycast(new Vector3(position.x, -29f, position.z), Vector3.down, out RaycastHit hit, 2f)
            && hit.collider == walkableWithWalls;
    }

    // Knockback must move even during the kick's stun. Using the normal queued
    // knockback would accumulate movement while CanMove is false.
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void MarshadowWallKickRPC(Vector3 direction)
    {
        if (pokemon.CurrentHp <= 0 || !CanBeKnockedBack) return;
        StartCoroutine(MarshadowWallKick(direction));
    }

    private IEnumerator MarshadowWallKick(Vector3 direction)
    {
        direction = MarshadowMoveRules.Direction(direction, transform.forward);
        float remaining = 2.5f;
        while (remaining > 0.01f && pokemon.CurrentHp > 0 && !pokemon.HasStatusEffect(StatusType.Unstoppable))
        {
            float requested = Mathf.Min(remaining, 12.5f * Time.deltaTime);
            Vector3 displacement = MoveWithWalls(direction * requested);
            float forwardTravel = Vector3.Dot(displacement, direction);
            if (requested > 0 && forwardTravel < requested * 0.4f)
            {
                ReportMarshadowWallRPC();
                break;
            }
            remaining -= Mathf.Max(0, forwardTravel);
            yield return null;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ReportMarshadowWallRPC()
    {
        if (Time.time > marshadowWallReportUntil || marshadowWallReportUntil == 0 || pokemon.CurrentHp <= 0) return;
        marshadowWallReportUntil = 0;
        if (pokemon.HasStatusEffect((ushort)62011))
            pokemon.UpdateStatusEffectTimeRPC(new StatusEffect(StatusType.Incapacitated, 0.2f, true, 62011), marshadowWallStun);
        else pokemon.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, marshadowWallStun, true, 62011));
        if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(marshadowWallAttacker, out var obj)
            && obj.TryGetComponent(out MovesController moves)) moves.MarshadowWallImpact(transform.position);
    }
}
