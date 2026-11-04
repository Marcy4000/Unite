using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NinetalesAvalancheEffect : PrefabMoveEffect
{
    [SerializeField] private GameObject wall, gust, collapse;
    [SerializeField] private BoxCollider blockingCollider;
    private bool gustFinished;
    public bool WallActive => blockingCollider != null && blockingCollider.enabled && blockingCollider.gameObject.activeInHierarchy;
    protected override void BeginPresentation()
    {
        if (wall != null) wall.SetActive(false);
        if (gust != null) gust.SetActive(false);
        if (collapse != null) collapse.SetActive(false);
    }
    protected override IEnumerator Execute()
    {
        Vector3 origin = Actor.transform.position, delta = Vector3.ClampMagnitude(Direction, NinetalesMoveRules.AvalancheRange);
        Vector3 forward = delta.sqrMagnitude > 0.001f ? delta.normalized : Actor.transform.forward;
        string animation = NinetalesMoveRules.Animation("spell1a");
        LockActions(); FaceRPC(forward); PlayAnimation(animation, NinetalesMoveRules.AvalancheWindup + NinetalesMoveRules.AvalancheRecovery, NinetalesMoveRules.AnimationBlend);
        yield return CastPause(NinetalesMoveRules.AvalancheWindup);
        if (CastInterrupted) yield break;
        Vector3 destination = origin + delta;
        PlaceWallRPC(destination, forward);
        var combat = Actor.MovesController.NinetalesCombat;
        StartCoroutine(TravelGust(origin, destination, forward, combat));
        float expires = Time.time + NinetalesMoveRules.WallLifetime;
        yield return CastPause(NinetalesMoveRules.AvalancheRecovery);
        UnlockActions();
        if (!CastInterrupted) Actor.AnimationManager.RecoverAnimationRPC(animation, NinetalesMoveRules.Animation("idle"), NinetalesMoveRules.AnimationBlend);
        while (Time.time < expires || !gustFinished) yield return null; // Cast recovery/CC never shorten an already placed wall.
        // S11B1 declares a collapse sphere growing from 1000 to 2500. Preserve
        // the existing project collapse lifetime, but sample growth, not a snapshot.
        var hit = new HashSet<Pokemon>(); float began = Time.time;
        while (true)
        {
            float progress = Mathf.Clamp01((Time.time - began) / NinetalesMoveRules.CollapseDuration);
            float radius = Mathf.Lerp(NinetalesMoveRules.CollapseStartRadius, NinetalesMoveRules.CollapseRadius, progress);
            CollapseRPC(radius); Physics.SyncTransforms();
            if (combat != null)
                foreach (var target in CombatTargeting.SphereTargets(destination + Vector3.up * 0.8f, radius))
                    HitOnce(target, hit, combat);
            if (progress >= 1) break;
            yield return null;
        }
    }

    private IEnumerator TravelGust(Vector3 origin, Vector3 destination, Vector3 forward, NinetalesRuntime combat)
    {
        var hit = new HashSet<Pokemon>(); Vector3 from = origin;
        while (true)
        {
            Vector3 to = Vector3.MoveTowards(from, destination, NinetalesMoveRules.WindSpeed * Time.deltaTime);
            GustRPC(to, forward, true); Physics.SyncTransforms();
            if (combat != null)
                foreach (var target in CombatTargeting.BoxTargets((from + to) * 0.5f + Vector3.up * 0.8f,
                    new Vector3(NinetalesMoveRules.AvalancheWidth * 0.5f, 1.5f,
                        (NinetalesMoveRules.AvalancheGustLength + Vector3.Distance(from, to)) * 0.5f), Quaternion.LookRotation(forward)))
                    HitOnce(target, hit, combat);
            if ((to - destination).sqrMagnitude < 0.0001f) break;
            from = to; yield return null;
        }
        GustRPC(destination, forward, false); gustFinished = true;
    }

    private void HitOnce(Pokemon target, HashSet<Pokemon> hit, NinetalesRuntime combat)
    {
        if (!CanHit(target) || !hit.Add(target)) return;
        if (combat.Hit(target, NinetalesMoveRules.AvalancheDamage(Actor.NetworkObjectId)) > 0 && Upgraded) combat.Slow(target, 50, 1);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PlaceWallRPC(Vector3 point, Vector3 forward)
    {
        transform.SetPositionAndRotation(point, Quaternion.LookRotation(forward));
        if (wall != null) wall.SetActive(true);
        if (blockingCollider != null)
        {
            // Players/Wall is ignored globally because lane movement uses a walkmesh.
            // This temporary wall must override that pair locally on every peer.
            int bodies = LayerMask.GetMask("Players", "WildPokemons");
            blockingCollider.includeLayers |= bodies;
            blockingCollider.excludeLayers &= ~bodies;
            blockingCollider.layerOverridePriority = Mathf.Max(1, blockingCollider.layerOverridePriority);
            blockingCollider.size = new Vector3(NinetalesMoveRules.WallWidth, NinetalesMoveRules.WallHeight, NinetalesMoveRules.WallThickness);
            blockingCollider.center = Vector3.up * (NinetalesMoveRules.WallHeight * 0.5f); blockingCollider.enabled = true;
        }
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void GustRPC(Vector3 point, Vector3 forward, bool active)
    {
        if (gust == null) return;
        gust.SetActive(active);
        if (!active) return;
        gust.transform.SetPositionAndRotation(point + Vector3.up * 0.15f, Quaternion.LookRotation(forward));
        gust.transform.localScale = new Vector3(NinetalesMoveRules.AvalancheWidth, 0.15f, NinetalesMoveRules.AvalancheGustLength);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void CollapseRPC(float radius)
    {
        if (wall != null) wall.SetActive(false);
        if (gust != null) gust.SetActive(false);
        if (collapse != null) { collapse.SetActive(true); collapse.transform.localScale = Vector3.one * radius; }
    }
}
