using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class UmbreonMeanLookEffect : UmbreonMoveEffect
{
    [SerializeField] private Transform field;
    private ushort pending, area;
    public bool Active { get; private set; }
    public Vector3 Center { get; private set; }
    protected override void BeginPresentation() { if (field != null) field.gameObject.SetActive(false); }
    protected override IEnumerator Execute()
    {
        if (!CanHit(Target)) yield break;
        pending = UmbreonEffectIds.Acquire(); area = UmbreonEffectIds.Acquire();
        LockActions(); FaceRPC(Direction); PlayAnimation(UmbreonMoveRules.Animation(Actor, "spell1a"), 0.4f);
        Target.AddStatusEffect(new StatusEffect(StatusType.UmbreonMeanLookPending, UmbreonMoveRules.MeanDelay + 0.1f, true, pending).WithSource(Actor.NetworkObjectId));
        Slow(Target, 30, 2);
        Vector3 from = Actor.transform.position + Vector3.up, to = Target.transform.position + Vector3.up;
        Actor.MovesController.ShowCombatVisualRPC(UmbreonMoveRules.Visuals + "Beam", from, to - from, new Vector3(0.12f, 0.12f, (to - from).magnitude), UmbreonMoveRules.MeanDelay);
        yield return CastPause(0.4f); UnlockActions();
        if (CastInterrupted) yield break;
        yield return new WaitForSeconds(Mathf.Max(0, UmbreonMoveRules.MeanDelay - 0.4f));
        if (!CanHit(Target) || !Target.HasStatusEffect(pending)) yield break;
        Target.RemoveStatusEffectWithID(pending);
        if (Combat.Zone != null && Combat.Zone != this) Combat.Zone.Despawn();
        Center = Target.transform.position; Active = true; Combat.Zone = this;
        float duration = UmbreonMoveRules.MeanDuration(Upgraded);
        Target.SetMovementAreaRPC(area, Center, UmbreonMoveRules.MeanRadius, NetworkManager.ServerTime.Time + duration, true);
        FormZoneRPC(Center);
        float until = Time.time + duration;
        while (Time.time < until && UmbreonMoveRules.Living(Target)) yield return null;
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void FormZoneRPC(Vector3 center)
    {
        transform.position = center;
        if (field != null) { field.gameObject.SetActive(true); field.localScale = Vector3.one * UmbreonMoveRules.MeanRadius; }
    }
    protected override void Cleanup()
    {
        Active = false;
        if (IsServer)
        {
            if (Target != null && Target.IsSpawned)
            {
                if (pending != 0) Target.RemoveStatusEffectWithID(pending);
                if (area != 0) Target.SetMovementAreaRPC(area, Center, 1, 0, false);
            }
            if (Combat != null && Combat.Zone == this) Combat.Zone = null;
            UmbreonEffectIds.Release(pending, 1); UmbreonEffectIds.Release(area);
        }
        base.Cleanup();
    }
}
