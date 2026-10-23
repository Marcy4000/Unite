using System.Collections;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerMovement
{
    private bool flygonDigTraversal, flygonDigHasSafe;
    private Vector3 flygonDigSafe;
    public bool IsFlygonDigging => flygonDigTraversal;
    public bool IsFlygonLandingValid(Vector3 point) => FlygonGroundValid(point);
    public void BeginFlygonDig()
    {
        if (flygonDigTraversal || walkableWithWalls == null || walkableNoWalls == null || !FlygonGroundValid(transform.position)) return;
        flygonDigSafe = transform.position; flygonDigHasSafe = true; flygonDigTraversal = true;
        currentMovement.y = 0; UpdateActiveWalkmesh();
    }
    private bool FlygonGroundValid(Vector3 point)
    {
        if (!FlygonMoveRules.Finite(point) || walkableWithWalls == null || walkableNoWalls == null) return false;
        bool Valid(Vector3 p) => walkableWithWalls.Raycast(new Ray(new Vector3(p.x, -29, p.z), Vector3.down), out _, 2)
            && walkableNoWalls.Raycast(new Ray(new Vector3(p.x, -39, p.z), Vector3.down), out _, 2);
        float radius = characterController != null ? (characterController.radius + characterController.skinWidth)
            * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z)) : 0.5f;
        return Valid(point) && Valid(point + Vector3.right * radius) && Valid(point - Vector3.right * radius)
            && Valid(point + Vector3.forward * radius) && Valid(point - Vector3.forward * radius);
    }
    private Vector3 MoveFlygonDig(Vector3 displacement)
    {
        if (!flygonDigTraversal || !FlygonMoveRules.Finite(displacement)) return Vector3.zero;
        Vector3 start = transform.position; displacement.y = 0;
        int steps = Mathf.Clamp(Mathf.CeilToInt(displacement.magnitude / 0.1f), 1, 128);
        for (int i = 1; i <= steps; i++)
        {
            Vector3 point = start + displacement * (i / (float)steps);
            if (!walkableNoWalls.Raycast(new Ray(new Vector3(point.x, -39, point.z), Vector3.down), out _, 2)) break;
            transform.position = point;
            if (FlygonGroundValid(point)) { flygonDigSafe = point; flygonDigHasSafe = true; }
        }
        return transform.position - start;
    }
    public Vector3 EndFlygonDig(bool reposition = true)
    {
        if (!flygonDigTraversal) return transform.position;
        if (reposition && !FlygonGroundValid(transform.position))
        {
            bool found = false; Vector3 requested = transform.position;
            for (int ring = 1; ring <= 8 && !found; ring++)
                for (int i = 0; i < 24; i++)
                {
                    float angle = i * Mathf.PI / 12;
                    Vector3 point = requested + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (ring * 0.25f);
                    if (FlygonGroundValid(point)) { transform.position = point; found = true; break; }
                }
            if (!found && flygonDigHasSafe) transform.position = flygonDigSafe;
        }
        flygonDigTraversal = flygonDigHasSafe = false; currentMovement.y = 0; UpdateActiveWalkmesh();
        return transform.position;
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void FlygonExitPositionRPC(Vector3 point)
    {
        EndFlygonDig(false);
        if (pokemon != null && pokemon.CurrentHp > 0 && FlygonGroundValid(point)) transform.position = point;
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void FlygonDisplaceRPC(Vector3 center, float distance, bool inward)
    {
        if (!FlygonMoveRules.Finite(center) || pokemon.CurrentHp <= 0 || !CanBeKnockedBack || pokemon.HasStatusEffect(StatusType.Unstoppable)
            || pokemon.HasStatusEffect(StatusType.Invincible) || pokemon.HasStatusEffect(StatusType.HindranceResistance)) return;
        Vector3 offset = inward ? center - transform.position : transform.position - center; offset.y = 0;
        float amount = Mathf.Clamp(distance, 0, 1.3f);
        if (inward) amount = Mathf.Min(amount, offset.magnitude);
        if (amount > 0.001f && offset.sqrMagnitude > 0.001f) StartCoroutine(FlygonDisplacement(offset.normalized, amount));
    }
    private IEnumerator FlygonDisplacement(Vector3 direction, float remaining)
    {
        while (remaining > 0.005f && pokemon.CurrentHp > 0 && !pokemon.HasStatusEffect(StatusType.Unstoppable)
            && !pokemon.HasStatusEffect(StatusType.Invincible) && !pokemon.HasStatusEffect(StatusType.HindranceResistance))
        {
            float step = Mathf.Min(remaining, 8 * Time.deltaTime);
            float moved = Vector3.Dot(MoveWithWalls(direction * step), direction);
            if (moved < step * 0.2f) yield break;
            remaining -= Mathf.Max(0, moved); yield return null;
        }
    }
}
