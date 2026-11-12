using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LucarioBoneRushEffect : LucarioMoveEffect
{
    [SerializeField] private Transform bone;
    private bool canRecast, catching, flying, caught;
    private Vector3 catchPosition;
    private Vector3 flightOrigin, flightDirection;
    private double flightBegan;
    public bool RecastOpen => canRecast;
    public override bool TryRecast(Vector3 direction)
    {
        if (!canRecast || catching || CastInterrupted) return false;
        catching = true; canRecast = false; return true;
    }
    protected override IEnumerator Execute()
    {
        LockActions(); FaceRPC(Direction);
        PlayAnimation("spell3b2", LucarioMoveRules.BoneStrikeInterval * 3);
        Actor.MovesController.ShowLucarioAttachedVisualRPC("Bone", "Bip001 R Hand", LucarioMoveRules.BoneStrikeInterval * 3);
        for (int i = 0; i < 3; i++)
        {
            if (i > 0) yield return CastPause(LucarioMoveRules.BoneStrikeInterval);
            if (CastInterrupted) yield break;
            Strike(LucarioMoveRules.BoneNearSize, 0.5f); Strike(LucarioMoveRules.BoneFarSize, 4);
        }
        yield return CastPause(LucarioMoveRules.BoneStrikeInterval); if (CastInterrupted) yield break;
        DetachRPC(Actor.transform.position);
        flightOrigin = Actor.transform.position + Vector3.up; flightDirection = Direction; flightBegan = NetworkManager.ServerTime.Time;
        LaunchBoneRPC(flightOrigin, flightDirection, flightBegan); PhaseRPC(1);
        PlayAnimation("spell3b3", LucarioMoveRules.BoneRecovery);
        UnlockActions(); canRecast = true; OfferRecast();
        Actor.MovesController.SetLucarioBoneExpiry(flightBegan + LucarioMoveRules.BoneWindow);
        var shoved = new HashSet<Pokemon>(); Vector3 previous = flightOrigin;
        while (!catching && !CastInterrupted && NetworkManager.ServerTime.Time < flightBegan + LucarioMoveRules.BoneWindow)
        {
            Vector3 point = BonePosition(); Physics.SyncTransforms();
            foreach (var target in CombatTargeting.SweptSphereTargets(previous, point, LucarioMoveRules.BoneRadius))
                if (CanHit(target) && shoved.Add(target)) Shove(target, Direction, LucarioMoveRules.BoneShove, 0.4f);
            previous = point; yield return null;
        }
        canRecast = false; Actor.MovesController.SetLucarioBoneExpiry(0);
        if (!catching || CastInterrupted) yield break;
        Vector3 destination = BonePosition() - Vector3.up;
        CatchBoneRPC(destination + Vector3.up);
        float duration = Mathf.Max(0.05f, Vector3.Distance(Actor.transform.position, destination) / LucarioMoveRules.BoneCatchSpeed);
        LockActions(); PlayAnimation("spell3b4", duration);
        Drive(destination, duration);
        yield return CastPause(duration); if (CastInterrupted) yield break;
        // Only a successful catch resets move one. Interrupted/invalid traversals do not.
        if (LucarioMoveRules.Flat(Actor.transform.position - destination).sqrMagnitude > 1) yield break;
        foreach (var target in CombatTargeting.SphereTargets(Actor.transform.position + Vector3.up, LucarioMoveRules.BoneCatchRadius))
            if (CanHit(target)) target.ResolveDamage(LucarioMoveRules.Bone(Actor.NetworkObjectId));
        if (Upgraded) Actor.MovesController.ResetLucarioFirstMove();
        DetachRPC(Actor.transform.position); flying = false; PhaseRPC(2); PlayAnimation("spell3b5", LucarioMoveRules.BoneRecovery);
        yield return CastPause(LucarioMoveRules.BoneRecovery); UnlockActions();
    }
    private void Strike(Vector3 size, float offset)
    {
        Physics.SyncTransforms();
        foreach (var target in CombatTargeting.BoxTargets(Actor.transform.position + Direction * offset + Vector3.up, size * 0.5f, Quaternion.LookRotation(Direction)))
            if (CanHit(target) && target.ResolveDamage(LucarioMoveRules.Bone(Actor.NetworkObjectId)) > 0) Combat.RestoreMarker(target);
    }
    private Vector3 BonePosition() => flightOrigin + flightDirection * Mathf.Clamp((float)(NetworkManager.ServerTime.Time - flightBegan) * LucarioMoveRules.BoneSpeed, 0, LucarioMoveRules.BoneRange);
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void LaunchBoneRPC(Vector3 origin, Vector3 direction, double began)
    { flightOrigin = origin; flightDirection = direction; flightBegan = began; flying = true; }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void CatchBoneRPC(Vector3 position) { caught = true; catchPosition = position; }
    private void LateUpdate()
    {
        if (!flying || bone == null || NetworkManager == null) return;
        bone.SetPositionAndRotation(caught ? catchPosition : BonePosition(), Quaternion.LookRotation(flightDirection) * Quaternion.Euler(0, 0, (float)(NetworkManager.ServerTime.Time - flightBegan) * 720));
    }
    protected override void Cleanup()
    {
        if (IsServer && Actor != null && Actor.IsSpawned) Actor.MovesController.SetLucarioBoneExpiry(0);
        base.Cleanup();
    }
}
