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
    private int finishedBlasts;
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
        StartCoroutine(TravelBlast(0));
        yield return CastPause(NinetalesMoveRules.BlastSeparation);
        if (!CastInterrupted) StartCoroutine(TravelBlast(1)); else finishedBlasts++;
        yield return CastPause(NinetalesMoveRules.WindRecovery);
        UnlockActions();
        if (!CastInterrupted) Actor.AnimationManager.RecoverAnimationRPC(animation, NinetalesMoveRules.Animation("idle", Actor.Pokemon.CurrentLevel >= 3), NinetalesMoveRules.AnimationBlend);
        while (finishedBlasts < 2) yield return null;
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

    private IEnumerator TravelBlast(int phase)
    {
        var combat = Actor.MovesController.NinetalesCombat;
        float range = blizzard ? NinetalesMoveRules.BlizzardRange : NinetalesMoveRules.IcyRange;
        float width = blizzard ? NinetalesMoveRules.BlizzardWidth : NinetalesMoveRules.IcyWidth;
        var hit = new HashSet<Pokemon>(); float travelled = 0;
        while (travelled < range)
        {
            float step = Mathf.Min(range - travelled, NinetalesMoveRules.WindSpeed * Time.deltaTime);
            Vector3 from = origin + Direction * travelled + Vector3.up * 0.8f;
            bool wall = false; RaycastHit contact = default;
            // First blast penetrates terrain. The second stops at any physical Wall,
            // including either team's Avalanche, without destroying that wall.
            if (blizzard && phase == 1)
            {
                Physics.SyncTransforms();
                wall = Physics.BoxCast(from, new Vector3(width * 0.5f, 0.6f, 0.02f), Direction, out contact,
                    Quaternion.LookRotation(Direction), step, LayerMask.GetMask("Wall"), QueryTriggerInteraction.Ignore);
                if (wall) step = Mathf.Max(0, contact.distance);
            }
            Vector3 to = from + Direction * step;
            BlastRPC(phase, to, width, true);
            if (combat != null)
                foreach (var target in CombatTargeting.BoxTargets((from + to) * 0.5f,
                    new Vector3(width * 0.5f, 1.2f, step * 0.5f + 0.12f), Quaternion.LookRotation(Direction)))
                {
                    if (!CanHit(target) || !hit.Add(target)) continue;
                    var damage = blizzard ? NinetalesMoveRules.BlizzardDamage(Actor.NetworkObjectId, Upgraded, phase) : NinetalesMoveRules.IcyDamage(Actor.NetworkObjectId);
                    if (combat.Hit(target, damage) <= 0) continue;
                    combat.Slow(target, (short)(blizzard ? 35 : 20), 1);
                    if (!blizzard || phase == 1)
                    {
                        float remaining = Mathf.Max(0, range - Vector3.Dot(target.transform.position - origin, Direction));
                        NinetalesMoveRules.Shove(target, Direction, Mathf.Min(NinetalesMoveRules.ShoveDistance, remaining));
                    }
                }
            travelled += step;
            if (wall) { impacted = true; impactPoint = origin + Direction * travelled; break; }
            yield return null;
        }
        BlastRPC(phase, Vector3.zero, width, false); finishedBlasts++;
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void BlastRPC(int phase, Vector3 position, float width, bool active)
    {
        if (blasts == null || phase >= blasts.Length || blasts[phase] == null) return;
        var blast = blasts[phase]; blast.SetActive(active);
        if (active) { blast.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Direction)); blast.transform.localScale = new Vector3(width, 1, 0.8f); }
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void FrostRPC(Vector3 point)
    {
        if (frost == null) return;
        frost.SetActive(true); frost.transform.position = point; frost.transform.localScale = Vector3.one * NinetalesMoveRules.FrostRadius;
    }
}
