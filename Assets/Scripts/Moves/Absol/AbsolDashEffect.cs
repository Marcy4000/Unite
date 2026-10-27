using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbsolDashEffect : PrefabMoveEffect
{
    [SerializeField] private AvailableMoves move;
    [SerializeField] private GameObject trail;
    private bool ready, recast;
    private Vector3 secondDirection;

    protected override void BeginPresentation() { if (trail != null) trail.SetActive(false); }

    public override bool TryRecast(Vector3 direction)
    {
        if (!ready || recast || !CombatTargeting.Finite(direction)) return false;
        ready = false; recast = true; secondDirection = direction; return true;
    }

    protected override IEnumerator Execute()
    {
        if (move == AvailableMoves.AbsolPursuit)
        {
            LockActions(); FaceRPC(Direction); PlayAnimation(AbsolMoveRules.PursuitAnimation,
                AbsolMoveRules.PursuitWindup + AbsolMoveRules.PursuitDuration + AbsolMoveRules.PursuitRecovery, AbsolMoveRules.MoveAnimationBlend);
            yield return CastPause(AbsolMoveRules.PursuitWindup);
            if (CastInterrupted) yield break;
            Drive(Actor.transform.position + Direction * AbsolMoveRules.PursuitDistance, AbsolMoveRules.PursuitDuration);
            yield return new WaitForSeconds(AbsolMoveRules.PursuitDuration);
            if (!CastInterrupted)
                (Actor.MovesController.ServerBasicAttack as AbsolBasicAtk)?.EmpowerPursuit(Upgraded);
            yield return CastPause(AbsolMoveRules.PursuitRecovery);
            UnlockActions(); RestoreIdle(); yield break;
        }

        bool feint = move == AvailableMoves.AbsolFeint;
        bool hit = false;
        float bow = feint ? (CastSequence % 2 == 1 ? AbsolMoveRules.DashBow : -AbsolMoveRules.DashBow) : -AbsolMoveRules.DashBow;
        yield return DashHit(Direction, bow, feint, false, success => hit = success);
        if (feint || !hit || Actor.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions)) yield break;
        yield return new WaitForSeconds(AbsolMoveRules.NightRecastDelay);
        ready = true; OfferRecast();
        float until = Time.time + AbsolMoveRules.NightRecastWindow;
        while (Time.time < until && !recast) yield return null;
        ready = false;
        if (recast) yield return DashHit(secondDirection, 0, false, true, _ => { });
    }

    private IEnumerator DashHit(Vector3 direction, float bow, bool feint, bool second, System.Action<bool> report)
    {
        LockActions(); FaceRPC(direction);
        float windup = feint ? AbsolMoveRules.FeintWindup : second ? AbsolMoveRules.NightSecondWindup : AbsolMoveRules.NightFirstWindup;
        float recovery = feint ? AbsolMoveRules.FeintRecovery : second ? AbsolMoveRules.NightSecondRecovery : AbsolMoveRules.NightFirstRecovery;
        PlayAnimation(feint ? AbsolMoveRules.FeintAnimation : second ? AbsolMoveRules.NightSecondAnimation : AbsolMoveRules.NightFirstAnimation,
            windup + AbsolMoveRules.DashDuration + recovery, AbsolMoveRules.MoveAnimationBlend);
        yield return CastPause(windup);
        if (CastInterrupted) yield break;
        TrailRPC(true);
        var hit = new HashSet<Pokemon>();
        bool confirmed = false;
        Vector3 previous = Actor.transform.position + Vector3.up * 0.8f;
        Drive(Actor.transform.position + direction * AbsolMoveRules.DashDistance, AbsolMoveRules.DashDuration, bow);
        float until = Time.time + AbsolMoveRules.DashDuration;
        do
        {
            yield return null;
            if (Actor.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions)) break;
            Physics.SyncTransforms();
            Vector3 current = Actor.transform.position + Vector3.up * 0.8f;
            foreach (var target in CombatTargeting.SweptSphereTargets(previous, current, AbsolMoveRules.DashRadius))
            {
                if (!CanHit(target) || !hit.Add(target)) continue;
                float missing = 1 - target.CurrentHp / (float)Mathf.Max(1, target.GetMaxHp());
                int dealt = target.ResolveDamage(AbsolMoveRules.DashDamage(Actor.NetworkObjectId, feint, second, missing));
                if (dealt <= 0) continue;
                confirmed = true;
                AbsolMoveRules.DashHaste(Actor.Pokemon);
                if (second && Upgraded) Actor.Pokemon.HealDamageRPC(Mathf.FloorToInt(dealt * 0.5f));
            }
            previous = current;
        } while (Time.time < until);
        TrailRPC(false);
        yield return CastPause(recovery);
        UnlockActions(); RestoreIdle(); report(confirmed && !CastInterrupted);
    }

    [Unity.Netcode.Rpc(Unity.Netcode.SendTo.Everyone, InvokePermission = Unity.Netcode.RpcInvokePermission.Server)]
    private void TrailRPC(bool active) { if (trail != null) trail.SetActive(active); }

    protected override void Update()
    {
        base.Update();
        if (Actor != null) transform.SetPositionAndRotation(Actor.transform.position, Actor.transform.rotation);
    }
    private void RestoreIdle()
    {
        if (Actor.Pokemon.CurrentHp > 0 && !Actor.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions)) PlayAnimation(AbsolMoveRules.Idle, blendDuration: AbsolMoveRules.RecoveryAnimationBlend);
    }
}
