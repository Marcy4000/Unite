using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class NinetalesAvalancheEffect : PrefabMoveEffect
{
    [SerializeField] private GameObject wall, gust, collapse;
    [SerializeField] private BoxCollider blockingCollider;
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
        PlaceWallRPC(destination, forward, origin);
        Physics.SyncTransforms();
        var combat = Actor.MovesController.NinetalesCombat;
        if (combat != null)
            foreach (var target in CombatTargeting.BoxTargets(origin + delta * 0.5f + Vector3.up,
                new Vector3(NinetalesMoveRules.AvalancheWidth * 0.5f, 1.5f, Mathf.Max(delta.magnitude, NinetalesMoveRules.WallThickness) * 0.5f), Quaternion.LookRotation(forward)))
                if (combat.Hit(target, NinetalesMoveRules.AvalancheDamage(Actor.NetworkObjectId)) > 0 && Upgraded) combat.Slow(target, 50, 1);
        float expires = Time.time + NinetalesMoveRules.WallLifetime;
        yield return CastPause(NinetalesMoveRules.AvalancheRecovery);
        UnlockActions();
        if (!CastInterrupted) Actor.AnimationManager.RecoverAnimationRPC(animation, NinetalesMoveRules.Animation("idle"), NinetalesMoveRules.AnimationBlend);
        while (Time.time < expires) yield return null; // Cast recovery/CC never shorten an already placed wall.
        CollapseRPC();
        if (combat != null)
            foreach (var target in CombatTargeting.SphereTargets(destination + Vector3.up * 0.8f, NinetalesMoveRules.CollapseRadius))
                if (combat.Hit(target, NinetalesMoveRules.AvalancheDamage(Actor.NetworkObjectId)) > 0 && Upgraded) combat.Slow(target, 50, 1);
        yield return new WaitForSeconds(0.25f);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PlaceWallRPC(Vector3 point, Vector3 forward, Vector3 origin)
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
        if (gust != null)
        {
            gust.SetActive(true); gust.transform.position = Vector3.Lerp(origin, point, 0.5f) + Vector3.up * 0.15f;
            gust.transform.localScale = new Vector3(NinetalesMoveRules.AvalancheWidth, 0.15f, Vector3.Distance(origin, point));
        }
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void CollapseRPC()
    {
        if (wall != null) wall.SetActive(false);
        if (gust != null) gust.SetActive(false);
        if (collapse != null) { collapse.SetActive(true); collapse.transform.localScale = Vector3.one * NinetalesMoveRules.CollapseRadius; }
    }
}
