using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LucarioAuraCannonEffect : LucarioMoveEffect
{
    [SerializeField] private Transform sphere;
    private bool launched;
    private Vector3 origin, flightDirection;
    private double began;
    public static float DistanceAt(float seconds)
    {
        float accelerationTime = (LucarioMoveRules.AuraMaxSpeed - LucarioMoveRules.AuraStartSpeed) / Mathf.Max(0.01f, LucarioMoveRules.AuraAcceleration);
        float t = Mathf.Clamp(seconds, 0, accelerationTime);
        return Mathf.Min(LucarioMoveRules.AuraRange, LucarioMoveRules.AuraStartSpeed * t + 0.5f * LucarioMoveRules.AuraAcceleration * t * t
            + Mathf.Max(0, seconds - accelerationTime) * LucarioMoveRules.AuraMaxSpeed);
    }
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction); Unstoppable(LucarioMoveRules.AuraWindup);
        PlayAnimation("spellg", LucarioMoveRules.AuraWindup);
        yield return CastPause(LucarioMoveRules.AuraWindup); if (CastInterrupted) yield break;
        Combat.UniteBuffs(); if (Actor.MovesController.HasLucarioMove(AvailableMoves.LucarioPowerUpPunch)) Combat.EmpowerAura();
        origin = Actor.transform.position + Vector3.up; flightDirection = Direction; began = NetworkManager.ServerTime.Time; DetachRPC(origin);
        LaunchRPC(origin, flightDirection, began); PhaseRPC(1); UnlockActions();
        var nextHit = new Dictionary<Pokemon, double>(); var counts = new Dictionary<Pokemon, int>();
        float distance = 0;
        while (distance < LucarioMoveRules.AuraRange && LucarioMoveRules.Living(Actor.Pokemon))
        {
            float next = DistanceAt((float)(NetworkManager.ServerTime.Time - began)); Physics.SyncTransforms();
            foreach (var target in CombatTargeting.SweptSphereTargets(origin + Direction * distance, origin + Direction * next, LucarioMoveRules.AuraRadius))
            {
                if (!CanHit(target) || (nextHit.TryGetValue(target, out var at) && NetworkManager.ServerTime.Time < at)
                    || (counts.TryGetValue(target, out var count) && count >= 4)) continue;
                nextHit[target] = NetworkManager.ServerTime.Time + LucarioMoveRules.AuraTickInterval;
                counts[target] = counts.TryGetValue(target, out var hits) ? hits + 1 : 1;
                if (target.ResolveDamage(LucarioMoveRules.Aura(Actor.NetworkObjectId)) > 0) Combat.RestoreMarker(target);
            }
            distance = next; transform.position = origin + Direction * distance; yield return null;
        }
        PhaseRPC(2); yield return new WaitForSeconds(0.15f);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void LaunchRPC(Vector3 start, Vector3 direction, double time) { origin = start; flightDirection = direction; began = time; launched = true; }
    private void LateUpdate()
    {
        if (launched && sphere != null && NetworkManager != null)
            sphere.SetPositionAndRotation(origin + flightDirection * DistanceAt((float)(NetworkManager.ServerTime.Time - began)), Quaternion.LookRotation(flightDirection));
    }
}
