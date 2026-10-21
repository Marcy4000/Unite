using System.Collections;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerMovement
{
    // Grounded, wall-respecting movement. Neither Dive nor Rain changes flying,
    // vertical root position, snap-to-ground, or another move's dash state.
    public Vector3 VaporeonHopDestination(Vector3 requested)
    {
        Vector3 start = transform.position;
        requested = VaporMoveRules.ClampDestination(start, requested, 4f);
        Vector3 last = start;
        for (int i = 1; i <= 40; i++)
        {
            Vector3 point = Vector3.Lerp(start, requested, i / 40f);
            if (walkableWithWalls != null && !walkableWithWalls.Raycast(new Ray(new Vector3(point.x, -29, point.z), Vector3.down), out _, 2)) break;
            last = point;
        }
        return last;
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
