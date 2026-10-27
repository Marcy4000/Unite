using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class AbsolSuckerPunchEffect : PrefabMoveEffect
{
    [SerializeField] private GameObject stanceVisual;
    private bool armed, countered, blocked;
    private float dodgeUntil;
    private int dodgeFrame = -1;
    private ushort TargetSlowId => (ushort)(60000 + NetworkObjectId % 5000);

    protected override IEnumerator Execute()
    {
        if (!CanHit(Target, true)) yield break;
        LockActions(false); FaceRPC(DirectionToTarget()); PlayAnimation(AbsolMoveRules.SuckerAnimation, AbsolMoveRules.SuckerStance, AbsolMoveRules.MoveAnimationBlend);
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, AbsolMoveRules.SuckerStance + AbsolMoveRules.SuckerDashDuration, true, AbsolMoveRules.SuckerStatusId));
        AbsolMoveRules.Refresh(Actor.Pokemon, new StatChange(50, Stat.Speed, AbsolMoveRules.SuckerStance, true, false, true, AbsolMoveRules.SuckerSelfSlowId));
        AbsolMoveRules.Refresh(Target, new StatChange(50, Stat.Speed, AbsolMoveRules.SuckerStance, true, false, true, TargetSlowId));
        armed = true;
        Actor.Pokemon.BeforeDamage += Dodge;
        Actor.Pokemon.BeforeStatChange += FilterHitStat;
        Actor.Pokemon.BeforeStatusEffect += FilterHitStatus;
        Target.OnOffensiveAction += TargetAttacks;
        float until = Time.time + AbsolMoveRules.SuckerStance;
        while (!countered && Time.time < until)
        {
            if (!TargetValid()) yield break;
            yield return null;
        }
        if (!TargetValid()) yield break;
        armed = false;
        if (countered) dodgeUntil = Time.time + AbsolMoveRules.SuckerDashDuration;
        Actor.Pokemon.RemoveAllStatChangeWithIDRPC(AbsolMoveRules.SuckerSelfSlowId);
        Target.RemoveAllStatChangeWithIDRPC(TargetSlowId);
        LockActions(); EndStanceRPC();
        Vector3 direction = DirectionToTarget(); FaceRPC(direction); PlayAnimation(AbsolMoveRules.SuckerAttackAnimation, AbsolMoveRules.SuckerDashDuration, AbsolMoveRules.MoveAnimationBlend);
        Vector3 destination = Target.transform.position - Target.transform.forward * AbsolMoveRules.SuckerBehindDistance;
        destination = Actor.PlayerMovement.GetWallCrossingDestination(destination);
        Drive(destination, AbsolMoveRules.SuckerDashDuration);
        yield return new WaitForSeconds(AbsolMoveRules.SuckerDashDuration);
        if (!TargetValid()) yield break;
        Physics.SyncTransforms();
        // The designated target still has to be close at contact; secondary
        // victims share the impact area rather than being global sure-hits.
        if (Vector3.Distance(Actor.transform.position, Target.transform.position) > AbsolMoveRules.SuckerRadius + 0.5f) yield break;
        Vector3 impact = Target.transform.position + Vector3.up * 0.8f;
        bool confirmed = false;
        foreach (var target in CombatTargeting.SphereTargets(impact, AbsolMoveRules.SuckerRadius))
        {
            if (!CanHit(target)) continue;
            var damage = AbsolMoveRules.SuckerDamage(Actor.NetworkObjectId, countered);
            if (target.ResolveDamage(damage) > 0)
            {
                confirmed = true;
                if (countered) AbsolMoveRules.Shove(target, direction, AbsolMoveRules.SuckerShoveDistance, AbsolMoveRules.SuckerShoveDuration);
            }
        }
        if (Upgraded && confirmed) AbsolMoveRules.Refresh(Actor.Pokemon, new StatChange(40, Stat.AtkSpeed, 3, true, true, false, AbsolMoveRules.SuckerAttackSpeedId));
        Actor.MovesController.ShowCombatVisualRPC(AbsolMoveRules.Visuals + "BoostedSlash", Target.transform.position, direction, Vector3.one * AbsolMoveRules.SuckerRadius, 0.25f);
        Actor.Pokemon.RemoveStatusEffectWithID(AbsolMoveRules.SuckerStatusId);
        yield return CastPause(AbsolMoveRules.SuckerRecovery);
        if (!CastInterrupted) PlayAnimation(AbsolMoveRules.Idle, blendDuration: AbsolMoveRules.RecoveryAnimationBlend);
    }

    private bool TargetValid() => CanHit(Target) && Vector3.Distance(Actor.transform.position, Target.transform.position) <= AbsolMoveRules.SuckerBreakRange;
    private Vector3 DirectionToTarget()
    {
        Vector3 direction = Target.transform.position - Actor.transform.position; direction.y = 0;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Actor.transform.forward;
    }
    private void TargetAttacks() { if (armed && TargetValid()) countered = true; }
    private bool Dodge(DamageInfo damage)
    {
        if (blocked || (!armed && Time.time > dodgeUntil) || !AbsolMoveRules.CanConsumeMark(damage)
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(damage.attackerId, out var obj)
            || !obj.TryGetComponent<Pokemon>(out var attacker) || attacker.TeamMember.IsOnSameTeam(Actor.CurrentTeam)) return false;
        countered = true; blocked = true; dodgeFrame = Time.frameCount; return true;
    }
    // Legacy hit payloads apply debuffs separately after damage. Suppress the
    // rejected hit's same-frame follow-up effects as well as its HP damage.
    private StatChange? FilterHitStat(StatChange change) => dodgeFrame == Time.frameCount && !change.IsBuff ? (StatChange?)null : change;
    private bool FilterHitStatus(StatusEffect effect) => dodgeFrame != Time.frameCount || !effect.IsNegativeStatus();

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void EndStanceRPC() { if (stanceVisual != null) stanceVisual.SetActive(false); }

    protected override void Update()
    {
        base.Update();
        if (Actor != null) transform.SetPositionAndRotation(Actor.transform.position, Actor.transform.rotation);
    }
    protected override void Cleanup()
    {
        if (!IsServer || Actor == null) return;
        Actor.Pokemon.BeforeDamage -= Dodge;
        Actor.Pokemon.BeforeStatChange -= FilterHitStat;
        Actor.Pokemon.BeforeStatusEffect -= FilterHitStatus;
        Actor.Pokemon.RemoveAllStatChangeWithIDRPC(AbsolMoveRules.SuckerSelfSlowId);
        Actor.Pokemon.RemoveStatusEffectWithID(AbsolMoveRules.SuckerStatusId);
        if (Actor.Pokemon.CurrentHp > 0 && !Actor.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions))
        {
            Actor.AnimationManager.RecoverAnimationRPC(AbsolMoveRules.SuckerAnimation, AbsolMoveRules.Idle, AbsolMoveRules.RecoveryAnimationBlend);
            Actor.AnimationManager.RecoverAnimationRPC(AbsolMoveRules.SuckerAttackAnimation, AbsolMoveRules.Idle, AbsolMoveRules.RecoveryAnimationBlend);
        }
        if (Target != null)
        {
            Target.OnOffensiveAction -= TargetAttacks;
            if (Target.IsSpawned) Target.RemoveAllStatChangeWithIDRPC(TargetSlowId);
        }
    }
}
