using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class EspeonFutureSightEffect : EspeonMoveEffect
{
    [SerializeField] private SphereCollider explosionArea;
    [SerializeField] private Transform markVisual;
    private ushort marker;
    private bool marked;
    public Pokemon MarkedTarget => Target;
    public bool UpgradedMark => Upgraded;
    public bool MarkIntact => marked && Target != null && Target.IsSpawned && Target.HasStatusEffect(marker);
    protected override void BeginPresentation()
    {
        markVisual.gameObject.SetActive(false);
        if (explosionArea != null && explosionArea.radius > 0) markVisual.localScale = Vector3.one * (EspeonMoveRules.FutureRadius / explosionArea.radius);
    }
    protected override IEnumerator Execute()
    {
        LockActions();
        FaceRPC(Target.transform.position - Actor.transform.position);
        // AGE S21 maps to spell2a, despite this being the Move 2 second branch.
        PlayAnimation(EspeonMoveRules.Animation(Actor, "spell2a"), 0.4f);
        yield return CastPause(0.15f);
        if (CastInterrupted || !CanHit(Target)) yield break;
        marker = EspeonEffectIds.Acquire();
        // Small expiry grace prevents script update order cancelling an on-time
        // explosion. The effect removes the marker at the exact 2s deadline.
        Target.AddStatusEffect(new StatusEffect(StatusType.FutureSightMarked, EspeonMoveRules.FutureDelay + 0.25f, true, marker).WithSource(Actor.NetworkObjectId));
        marked = Target.HasStatusEffect(marker);
        UnlockActions();
        if (!marked) yield break;
        Combat.Mark(this);
        Actor.MovesController.SetEspeonMark(Target.NetworkObjectId, true);
        MarkRPC(marker);
        float until = Time.time + EspeonMoveRules.FutureDelay;
        while (Time.time < until)
        {
            if (!MarkIntact || !EspeonMoveRules.Living(Target)) yield break;
            yield return null;
        }
        if (!MarkIntact || !EspeonMoveRules.Living(Target)) yield break;
        Vector3 center = Target.transform.position + Vector3.up * 0.8f;
        if (explosionArea != null) explosionArea.radius = EspeonMoveRules.FutureRadius;
        Physics.SyncTransforms();
        foreach (var target in CombatTargeting.SphereTargets(center, EspeonMoveRules.FutureRadius))
        {
            if (!CanHit(target)) continue;
            DamageInfo damage = AddFlat(EspeonMoveRules.Future(Actor.NetworkObjectId), 0.12f * (target.GetMaxHp() - target.CurrentHp));
            int calculated = target.CalculateDamage(damage, Actor.Pokemon);
            if (target.Type != PokemonType.Player && calculated > 1800) damage.multiplier = 1800f / calculated;
            Hit(target, damage);
        }
        Visual("Burst", center, Vector3.forward, Vector3.one * EspeonMoveRules.FutureRadius, 0.4f);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void MarkRPC(ushort id) { marker = id; marked = true; markVisual.gameObject.SetActive(true); }
    protected override void Update()
    {
        base.Update();
        if (Target != null && marked)
        {
            transform.position = Target.transform.position;
            markVisual.Rotate(Vector3.up, 75 * Time.deltaTime);
        }
    }
    protected override void Cleanup()
    {
        if (IsServer)
        {
            if (Actor != null) { Actor.MovesController.ClearEspeonFuture(this); Actor.MovesController.ClearEspeonMark(Target); }
            if (Target != null && Target.IsSpawned && marker != 0) Target.RemoveStatusEffectWithID(marker);
            if (marker != 0) EspeonEffectIds.Release(marker);
        }
        marked = false;
        base.Cleanup();
    }
}
