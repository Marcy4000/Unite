using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class Pokemon
{
    // Synchronous server hooks: interception must precede movement or shield storage.
    public event Func<bool> BeforeDisplacement;
    public static event Func<Pokemon, ShieldInfo, ShieldInfo> BeforeShieldGrant;
    private struct MovementArea { public Vector3 Center; public float Radius; public double Until; }
    private readonly Dictionary<ushort, MovementArea> movementAreas = new Dictionary<ushort, MovementArea>();
    public bool RejectDisplacement()
    {
        if (!IsServer) return false;
        if (BeforeDisplacement != null)
            foreach (Func<bool> filter in BeforeDisplacement.GetInvocationList()) if (!filter()) return true;
        return false;
    }
    private ShieldInfo InterceptShieldGrant(ShieldInfo info)
    {
        if (BeforeShieldGrant != null)
            foreach (Func<Pokemon, ShieldInfo, ShieldInfo> filter in BeforeShieldGrant.GetInvocationList()) info = filter(this, info);
        return info;
    }
    /// <summary>Remove a fraction without damaging HP, refreshing timers, or firing damage procs.</summary>
    public int ExtractEligibleShields(float fraction)
    {
        if (!IsServer || CurrentHp <= 0) return 0;
        int total = 0;
        for (int i = shields.Count - 1; i >= 0; i--)
        {
            ShieldInfo info = shields[i];
            if (!UmbreonMoveRules.Stealable(info)) continue;
            int amount = Mathf.FloorToInt(info.Amount * Mathf.Clamp01(fraction));
            total += amount; info.Amount -= amount;
            if (info.Amount <= 0) { shields.RemoveAt(i); shieldTimers.RemoveAt(i); }
            else shields[i] = info;
        }
        return total;
    }
    // Stolen shield contributions share one absolute deadline; AddShieldRPC's
    // normal same-ID duration extension must not accumulate those deadlines.
    public void AddStolenShield(int amount, ushort id, double deadline)
    {
        if (!IsServer || amount <= 0 || CurrentHp <= 0) return;
        float duration = (float)(deadline - NetworkManager.ServerTime.Time);
        if (duration <= 0) return;
        for (int i = 0; i < shields.Count; i++)
            if (shields[i].ID == id)
            {
                var info = shields[i]; info.Amount += amount; info.Duration = duration;
                shields[i] = info; shieldTimers[i] = duration; return;
            }
        shields.Add(new ShieldInfo(amount, id, 0, duration, true).WithSource(NetworkObjectId));
        shieldTimers.Add(duration);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void SetMovementAreaRPC(ushort id, Vector3 center, float radius, double until, bool active)
    {
        if (!active) { movementAreas.Remove(id); return; }
        if (!CombatTargeting.Finite(center) || !float.IsFinite(radius) || radius <= 0) return;
        movementAreas[id] = new MovementArea { Center = center, Radius = radius, Until = until };
    }
    public Vector3 ConstrainMovement(Vector3 point)
    {
        if (movementAreas.Count == 0 || CurrentHp <= 0 || NetworkManager == null) return point;
        // Iteration handles the intersection of multiple independently owned zones.
        for (int pass = 0; pass < 3; pass++)
            foreach (var area in movementAreas.Values)
            {
                if (NetworkManager.ServerTime.Time >= area.Until) continue;
                Vector3 offset = UmbreonMoveRules.Flat(point - area.Center);
                if (offset.sqrMagnitude > area.Radius * area.Radius)
                { float y = point.y; point = area.Center + offset.normalized * area.Radius; point.y = y; }
            }
        return point;
    }
    private void EnforceMovementAreas()
    {
        if (movementAreas.Count == 0 || !IsSpawned || (!IsOwner && !IsServer)) return;
        if (CurrentHp <= 0) { movementAreas.Clear(); return; }
        foreach (var id in new List<ushort>(movementAreas.Keys))
            if (NetworkManager.ServerTime.Time >= movementAreas[id].Until) movementAreas.Remove(id);
        transform.position = ConstrainMovement(transform.position);
    }
}
