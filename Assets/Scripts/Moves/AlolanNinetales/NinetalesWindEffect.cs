using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NinetalesWindEffect : PrefabMoveEffect
{
    [SerializeField] private bool blizzard;
    [SerializeField] private GameObject[] blasts;
    [SerializeField] private GameObject frost;
    private Vector3 origin;
    private bool impacted;
    private Vector3 impactPoint;
    private bool secondFinished;
    protected override void BeginPresentation()
    {
        foreach (var blast in blasts) if (blast != null) blast.SetActive(false);
        if (frost != null) frost.SetActive(false);
    }
    protected override IEnumerator Execute()
    {
        origin = Actor.transform.position;
        string animation = NinetalesMoveRules.Animation(blizzard ? "spell2a" : "spell2", Actor.Pokemon.CurrentLevel >= 3);
        float castTime = NinetalesMoveRules.WindWindup + NinetalesMoveRules.BlastSeparation + NinetalesMoveRules.WindRecovery;
        LockActions(); FaceRPC(Direction); PlayAnimation(animation, castTime, NinetalesMoveRules.AnimationBlend);
        yield return CastPause(NinetalesMoveRules.WindWindup);
        if (CastInterrupted) yield break;
        // S2B1/S21B1 declare stationary rectangular hit volumes. Only Blizzard's
        // second phase has a movement declaration; the first is not a projectile.
        HitLine(0);
        yield return CastPause(NinetalesMoveRules.BlastSeparation);
        if (!CastInterrupted)
        {
            if (blizzard) StartCoroutine(TravelBlizzard());
            else { HitLine(1); secondFinished = true; }
        }
        else secondFinished = true;
        yield return CastPause(NinetalesMoveRules.WindRecovery);
        UnlockActions();
        if (!CastInterrupted) Actor.AnimationManager.RecoverAnimationRPC(animation, NinetalesMoveRules.Animation("idle", Actor.Pokemon.CurrentLevel >= 3), NinetalesMoveRules.AnimationBlend);
        BlastRPC(0, Vector3.zero, Vector3.one, false);
        if (!blizzard) BlastRPC(1, Vector3.zero, Vector3.one, false);
        while (!secondFinished) yield return null;
        if (!impacted) yield break;
        FrostRPC(impactPoint);
        var combat = Actor.MovesController.NinetalesCombat;
        float start = Time.time;
        for (int tick = 0; tick < NinetalesMoveRules.FrostTicks; tick++)
        {
            while (Time.time < start + (tick + 1) * NinetalesMoveRules.FrostInterval) yield return null;
            Physics.SyncTransforms();
            if (combat == null) continue;
            foreach (var target in CombatTargeting.SphereTargets(impactPoint + Vector3.up * 0.8f, NinetalesMoveRules.FrostRadius))
                if (combat.Hit(target, NinetalesMoveRules.BlizzardDamage(Actor.NetworkObjectId, Upgraded, 2)) > 0) combat.Slow(target, 35, 1);
        }
    }

    private void HitLine(int phase)
    {
        var combat = Actor.MovesController.NinetalesCombat;
        float range = blizzard ? NinetalesMoveRules.BlizzardRange : NinetalesMoveRules.IcyRange;
        float width = blizzard ? NinetalesMoveRules.BlizzardWidth : NinetalesMoveRules.IcyCollisionWidth;
        Vector3 center = origin + Direction * (range * 0.5f) + Vector3.up * 0.8f;
        BlastRPC(phase, center, new Vector3(width, 1, range), true);
        Physics.SyncTransforms();
        if (combat == null) return;
        foreach (var target in CombatTargeting.BoxTargets(center, new Vector3(width * 0.5f, 1.2f, range * 0.5f), Quaternion.LookRotation(Direction)))
        {
            var damage = blizzard ? NinetalesMoveRules.BlizzardDamage(Actor.NetworkObjectId, Upgraded, phase) : NinetalesMoveRules.IcyDamage(Actor.NetworkObjectId);
            if (combat.Hit(target, damage) <= 0) continue;
            combat.Slow(target, (short)(blizzard ? 35 : 20), 1);
            if (!blizzard) Shove(target, range);
        }
    }

    private void Shove(Pokemon target, float range)
    {
        float remaining = Mathf.Max(0, range - Vector3.Dot(target.transform.position - origin, Direction));
        NinetalesMoveRules.Shove(target, Direction, Mathf.Min(NinetalesMoveRules.ShoveDistance, remaining));
    }

    private IEnumerator TravelBlizzard()
    {
        var combat = Actor.MovesController.NinetalesCombat;
        float range = NinetalesMoveRules.BlizzardRange, radius = NinetalesMoveRules.FrostRadius;
        var hit = new HashSet<Pokemon>(); float travelled = 0;
        // Sample the initial sphere as well, including walls already overlapping it.
        while (true)
        {
            float step = Mathf.Min(range - travelled, NinetalesMoveRules.WindSpeed * Time.deltaTime);
            Vector3 from = origin + Direction * travelled + Vector3.up * 0.8f;
            Physics.SyncTransforms();
            bool wall = Physics.CheckSphere(from, radius, LayerMask.GetMask("Wall"), QueryTriggerInteraction.Ignore);
            if (wall) step = 0;
            else if (Physics.SphereCast(from, radius, Direction, out var contact, step, LayerMask.GetMask("Wall"), QueryTriggerInteraction.Ignore))
            {
                wall = true; step = Mathf.Max(0, contact.distance);
            }
            Vector3 to = from + Direction * step;
            BlastRPC(1, to, new Vector3(radius * 2, 1, radius * 2), true);
            if (combat != null)
                foreach (var target in CombatTargeting.SweptSphereTargets(from, to, radius))
                {
                    if (!CanHit(target) || !hit.Add(target)) continue;
                    if (combat.Hit(target, NinetalesMoveRules.BlizzardDamage(Actor.NetworkObjectId, Upgraded, 1)) <= 0) continue;
                    combat.Slow(target, 35, 1);
                    Shove(target, range);
                }
            travelled += step;
            if (wall) { impacted = true; impactPoint = origin + Direction * travelled; break; }
            if (travelled >= range) break;
            yield return null;
        }
        BlastRPC(1, Vector3.zero, Vector3.one, false); secondFinished = true;
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void BlastRPC(int phase, Vector3 position, Vector3 size, bool active)
    {
        if (blasts == null || phase >= blasts.Length || blasts[phase] == null) return;
        var blast = blasts[phase]; blast.SetActive(active);
        if (active) { blast.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Direction)); blast.transform.localScale = size; }
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void FrostRPC(Vector3 point)
    {
        if (frost == null) return;
        frost.SetActive(true); frost.transform.position = point; frost.transform.localScale = Vector3.one * NinetalesMoveRules.FrostRadius;
    }
}
