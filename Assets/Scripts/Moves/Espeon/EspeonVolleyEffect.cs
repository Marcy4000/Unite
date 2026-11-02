using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class EspeonVolleyEffect : EspeonMoveEffect
{
    [SerializeField] private Transform[] projectiles;
    [SerializeField] private SphereCollider projectileHitbox;
    private readonly Dictionary<Pokemon, int> hits = new Dictionary<Pokemon, int>();
    private HashSet<Pokemon>[] pierced;
    private Vector3[] positions, directions;
    private float[] travelled, focusDistances;
    private bool[] flying, homingFinished;
    private Pokemon homing;
    private float speed;
    private bool empowered, running;
    protected override void BeginPresentation()
    {
        foreach (var bolt in projectiles) bolt.gameObject.SetActive(false);
    }
    protected override IEnumerator Execute()
    {
        LockActions();
        bool psyshock = MoveId == AvailableMoves.EspeonPsyshock;
        FaceRPC(Direction);
        PlayAnimation(EspeonMoveRules.Animation(Actor, psyshock ? "spell1a" : "spell1"), 0.55f);
        yield return CastPause(0.25f);
        if (CastInterrupted) yield break;
        Vector3 focal = Actor.transform.position + Direction;
        Pokemon mark = Combat.MarkedTarget;
        bool tracks = psyshock && EspeonMoveRules.CanHit(Actor, mark)
            && EspeonMoveRules.Flat(mark.transform.position - focal).sqrMagnitude <= EspeonMoveRules.FutureRadius * EspeonMoveRules.FutureRadius;
        PrepareRPC(Actor.transform.position + Vector3.up * 0.8f, focal + Vector3.up * 0.8f,
            psyshock ? 5 : 3, tracks ? mark.NetworkObjectId : ulong.MaxValue, Empowered,
            psyshock && Upgraded ? EspeonMoveRules.UpgradedProjectileSpeed : EspeonMoveRules.ProjectileSpeed);
        yield return new WaitForSeconds(0.05f);
        UnlockActions();
        while (running) yield return null;
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PrepareRPC(Vector3 origin, Vector3 focal, int count, ulong mark, bool enhanced, float projectileSpeed)
    {
        empowered = enhanced; speed = projectileSpeed;
        homing = NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(mark, out var obj) ? obj.GetComponent<Pokemon>() : null;
        Vector3 forward = EspeonMoveRules.Flat(focal - origin);
        if (forward.sqrMagnitude < 0.001f) forward = Actor.transform.forward;
        positions = new Vector3[count]; directions = new Vector3[count]; travelled = new float[count];
        focusDistances = new float[count]; flying = new bool[count]; running = true;
        pierced = new HashSet<Pokemon>[count]; homingFinished = new bool[count];
        if (projectileHitbox != null) projectileHitbox.radius = EspeonMoveRules.ProjectileRadius;
        for (int i = 0; i < projectiles.Length; i++)
        {
            projectiles[i].gameObject.SetActive(i < count);
            if (i >= count) continue;
            pierced[i] = new HashSet<Pokemon>();
            positions[i] = EspeonMoveRules.ProjectileOrigin(origin, forward, i, count);
            Vector3 delta = focal - positions[i]; focusDistances[i] = delta.magnitude;
            directions[i] = delta.sqrMagnitude > 0.001f ? delta.normalized : forward.normalized;
            flying[i] = true; projectiles[i].position = positions[i];
        }
    }
    protected override void Update()
    {
        base.Update();
        if (!running || Actor == null) return;
        bool any = false;
        for (int i = 0; i < flying.Length; i++)
        {
            if (!flying[i]) continue;
            any = true;
            Vector3 from = positions[i];
            if (!homingFinished[i] && EspeonMoveRules.Living(homing))
            {
                Vector3 delta = homing.transform.position + Vector3.up * 0.8f - from;
                if (delta.sqrMagnitude > 0.001f) directions[i] = delta.normalized;
                focusDistances[i] = travelled[i] + delta.magnitude;
            }
            float limit = empowered ? focusDistances[i] : EspeonMoveRules.ProjectileDistance;
            float step = Mathf.Min(speed * Time.deltaTime, Mathf.Max(0, limit - travelled[i]));
            positions[i] += directions[i] * step; travelled[i] += step;
            projectiles[i].position = positions[i];
            if (!IsServer) continue;
            if (!empowered)
            {
                foreach (var target in CombatTargeting.SweptSphereTargets(from, positions[i], EspeonMoveRules.ProjectileRadius))
                {
                    if (!CanHit(target) || !pierced[i].Add(target)) continue;
                    ApplyHit(target);
                    // Keep travelling forward after piercing the marked target,
                    // rather than steering back into it on subsequent frames.
                    if (target == homing) FinishHomingRPC(i);
                }
            }
            if (travelled[i] + 0.001f >= limit)
            {
                if (empowered)
                {
                    foreach (var target in CombatTargeting.SphereTargets(positions[i], EspeonMoveRules.FocusRadius)) ApplyHit(target);
                    Visual("Burst", positions[i], Vector3.forward, Vector3.one * EspeonMoveRules.FocusRadius, 0.3f);
                }
                StopBoltRPC(i);
            }
        }
        if (IsServer && !any) running = false;
    }
    private void ApplyHit(Pokemon target)
    {
        bool psyshock = MoveId == AvailableMoves.EspeonPsyshock;
        hits.TryGetValue(target, out int count);
        if (Hit(target, EspeonMoveRules.Volley(Actor.NetworkObjectId, psyshock, count > 0), psyshock) <= 0) return;
        hits[target] = ++count;
        if (psyshock) { Slow(target, Mathf.Min(75, count * 15), 3); if (count == 5) Stun(target, 1.25f); }
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void FinishHomingRPC(int index) { homingFinished[index] = true; }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void StopBoltRPC(int index) { flying[index] = false; projectiles[index].gameObject.SetActive(false); }
}
