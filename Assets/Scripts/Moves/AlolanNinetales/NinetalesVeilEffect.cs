using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NinetalesVeilEffect : PrefabMoveEffect
{
    [SerializeField] private GameObject aurora;
    private readonly HashSet<Pokemon> recipients = new HashSet<Pokemon>();
    private ushort reductionId;
    private NinetalesRuntime combat;
    public Vector3 Center { get; private set; }
    protected override void BeginPresentation() { if (aurora != null) aurora.SetActive(false); }
    protected override IEnumerator Execute()
    {
        reductionId = NinetalesEffectIds.Acquire(); combat = Actor.MovesController.NinetalesCombat;
        string animation = NinetalesMoveRules.Animation("spell2b");
        LockActions(); FaceRPC(Direction);
        PlayAnimation(animation, NinetalesMoveRules.VeilWindup + NinetalesMoveRules.VeilDashDuration + NinetalesMoveRules.VeilRecovery, NinetalesMoveRules.AnimationBlend);
        yield return CastPause(NinetalesMoveRules.VeilWindup);
        if (CastInterrupted) yield break;
        Vector3 destination = Actor.PlayerMovement.GetWallCrossingDestination(Actor.transform.position + Direction * NinetalesMoveRules.VeilDash);
        Drive(destination, NinetalesMoveRules.VeilDashDuration);
        yield return CastPause(NinetalesMoveRules.VeilDashDuration);
        if (CastInterrupted) yield break;
        Center = Actor.transform.position; ShowAuroraRPC(Center);
        combat?.EmpowerVeil();
        float expires = Time.time + NinetalesMoveRules.VeilDuration;
        UpdateRecipients();
        yield return CastPause(NinetalesMoveRules.VeilRecovery);
        UnlockActions();
        if (!CastInterrupted) Actor.AnimationManager.RecoverAnimationRPC(animation, NinetalesMoveRules.Animation("idle"), NinetalesMoveRules.AnimationBlend);
        while (Time.time < expires) { UpdateRecipients(); yield return null; }
    }
    private void UpdateRecipients()
    {
        Physics.SyncTransforms();
        var inside = new HashSet<Pokemon>();
        foreach (var target in CombatTargeting.SphereTargets(Center + Vector3.up * 0.8f, NinetalesMoveRules.VeilRadius))
            if (NinetalesMoveRules.Living(target) && target.Type == PokemonType.Player && target.TeamMember.IsOnSameTeam(Actor.CurrentTeam)) inside.Add(target);
        foreach (var target in new List<Pokemon>(recipients))
            if (!inside.Contains(target)) { if (target != null && target.IsSpawned) target.RemoveAllStatChangeWithIDRPC(reductionId); recipients.Remove(target); }
        foreach (var target in inside)
        {
            recipients.Add(target);
            if (!target.HasStatChangeWithID(reductionId)) target.AddStatChange(new StatChange((short)(Upgraded ? 40 : 35), Stat.DamageReduction, 0, false, true, false, reductionId).WithSource(Actor.NetworkObjectId));
        }
        // Only this Ninetales' own veil enables its boosted attacks/movement speed.
        combat?.SetInside(this, inside.Contains(Actor.Pokemon));
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void ShowAuroraRPC(Vector3 point)
    {
        transform.position = point;
        if (aurora != null) { aurora.SetActive(true); aurora.transform.localScale = Vector3.one * NinetalesMoveRules.VeilRadius; }
    }
    protected override void Cleanup()
    {
        if (!IsServer) return;
        foreach (var target in recipients) if (target != null && target.IsSpawned) target.RemoveAllStatChangeWithIDRPC(reductionId);
        recipients.Clear(); combat?.SetInside(this, false);
        NinetalesEffectIds.Release(reductionId);
    }
}
