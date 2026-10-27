using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbsolBasicAtk : ChargedBasicAttackBase
{
    private int charge;
    private float pursuitUntil;
    private Coroutine attackRoutine;
    private bool interrupted;
    private readonly Dictionary<Pokemon, GameObject> backs = new Dictionary<Pokemon, GameObject>();
    protected override int AttackCharge => charge;
    public override bool ServerResolved => true;
    public override float MoveInputBufferDuration => AbsolMoveRules.BasicInputBuffer;
    public int Charge => charge;
    public bool PursuitReady => Time.time < pursuitUntil;

    public override void Initialize(PlayerManager manager) { range = AbsolMoveRules.BasicRange; base.Initialize(manager); }
    public override void InitializeServer(PlayerManager manager) { base.InitializeServer(manager); range = AbsolMoveRules.BasicRange; }
    public override void ApplyServerState(int value, float until) { charge = value; pursuitUntil = until; range = PursuitReady ? AbsolMoveRules.PursuitRange : AbsolMoveRules.BasicRange; }

    public void EmpowerPursuit(bool upgraded)
    {
        pursuitUntil = Time.time + AbsolMoveRules.PursuitEmpowerDuration;
        if (upgraded) AbsolMoveRules.Refresh(playerManager.Pokemon, new StatChange(35, Stat.Speed, AbsolMoveRules.PursuitSpeedDuration, true, true, true, AbsolMoveRules.PursuitSpeedId));
        Synchronize();
    }

    private void Synchronize() => playerManager.MovesController.SynchronizeBasicAttackRPC(charge, Mathf.Max(0, pursuitUntil - Time.time));

    public override void Perform(bool wildPriority)
    {
        range = PursuitReady ? AbsolMoveRules.PursuitRange : AbsolMoveRules.BasicRange;
        var target = Aim.Instance.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target == null && (PursuitReady || playerManager.PlayerMovement.IsMoving)) return;
        playerManager.MovesController.RequestServerBasicAttack(target != null ? target.GetComponent<Pokemon>().NetworkObjectId : ulong.MaxValue);
    }

    public override void PerformOnServer(Pokemon target)
    {
        if (attackRoutine != null) return;
        float reach = PursuitReady ? AbsolMoveRules.PursuitRange : AbsolMoveRules.BasicRange;
        if (target != null && (!AbsolMoveRules.CanHit(playerManager, target) || !CombatTargeting.WithinMeleeRange(playerManager.transform.position, target.transform.position, reach))) return;
        if (PursuitReady && target == null) return;
        attackRoutine = playerManager.StartCoroutine(Attack(target));
    }

    private IEnumerator Attack(Pokemon primary)
    {
        bool pursuit = PursuitReady && primary != null;
        bool boosted = charge == 2;
        interrupted = false;
        float scale = AbsolMoveRules.BasicTimingScale(playerManager.MovesController.GetAtkSpeedCooldown());
        float windup = (pursuit ? AbsolMoveRules.PursuitAttackWindup : boosted ? AbsolMoveRules.BoostedWindup : AbsolMoveRules.BasicWindup) * scale;
        float recovery = (pursuit ? AbsolMoveRules.PursuitAttackRecovery : boosted ? AbsolMoveRules.BoostedRecovery : AbsolMoveRules.BasicRecovery) * scale;
        float bonusDelay = boosted && !pursuit ? AbsolMoveRules.BoostedHitDelay * scale : 0;
        float leapDuration = AbsolMoveRules.PursuitLeapDuration * scale;
        Vector3 start = playerManager.transform.position;
        Vector3 direction = primary != null ? primary.transform.position - start : playerManager.transform.forward;
        direction.y = 0; if (direction.sqrMagnitude < 0.001f) direction = playerManager.transform.forward;
        direction.Normalize();
        string animation = pursuit ? AbsolMoveRules.PursuitAttackAnimation : boosted ? "ani_atk3_bat_0359_XYZFixed" : charge == 0 ? "ani_atk1_bat_0359_XYZFixed" : "ani_atk2_bat_0359_XYZFixed";
        playerManager.MovesController.PresentServerAttackRPC(true, direction, animation,
            windup + (pursuit ? leapDuration : bonusDelay) + recovery, blendDuration: AbsolMoveRules.BasicAnimationBlend * scale);
        try
        {
            yield return AttackPause(windup);
            if (AttackInterrupted) yield break;
            if (pursuit)
            {
                pursuitUntil = 0; charge = 0;
                playerManager.Pokemon.RemoveAllStatChangeWithIDRPC(AbsolMoveRules.PursuitSpeedId);
                Synchronize();
                // Reuse the same owner/server driven path as prefab effects.
                Vector3 destination = playerManager.PlayerMovement.GetWallCrossingDestination(primary.transform.position - direction * 0.6f);
                playerManager.MovesController.DriveBasicAttackRPC(start, destination, leapDuration);
                yield return AttackPause(leapDuration);
            }
            if (AttackInterrupted) yield break;
            Physics.SyncTransforms();
            var hits = new List<Pokemon>();
            if (pursuit)
            {
                if (AbsolMoveRules.CanHit(playerManager, primary)
                    && CombatTargeting.WithinMeleeRange(playerManager.transform.position, primary.transform.position, AbsolMoveRules.BasicRange))
                    foreach (var target in CombatTargeting.SphereTargets(primary.transform.position + Vector3.up * 0.8f, AbsolMoveRules.PursuitSplashRadius))
                        if (AbsolMoveRules.CanHit(playerManager, target)) hits.Add(target);
            }
            else foreach (var target in CombatTargeting.SectorTargets(playerManager.transform.position, direction, AbsolMoveRules.BasicRange, AbsolMoveRules.BasicAngle))
                if (AbsolMoveRules.CanHit(playerManager, target)) hits.Add(target);

            bool backHit = false;
            Pokemon isolated = null;
            foreach (var target in hits)
            {
                bool rear = pursuit && AbsolMoveRules.IsBackstab(playerManager.transform.position, target.transform.position, target.transform.forward);
                var damage = pursuit ? AbsolMoveRules.PursuitDamage(playerManager.NetworkObjectId, rear) : AbsolMoveRules.BasicDamage(playerManager.NetworkObjectId, boosted, false);
                int dealt = target.ResolveDamage(damage);
                if (dealt <= 0) continue;
                if (rear) backHit = true;
                if (!pursuit && boosted) AbsolMoveRules.Refresh(target, new StatChange(15, Stat.Defense, AbsolMoveRules.BoostedDefenseDuration, true, false, true, AbsolMoveRules.DefenseId));
                if (!pursuit && boosted && hits.Count == 1) isolated = target;
            }
            if (backHit) playerManager.MovesController.ReduceMoveCooldownByFraction(MoveType.MoveA, AbsolMoveRules.PursuitCooldownReduction);
            playerManager.MovesController.ShowCombatVisualRPC(AbsolMoveRules.Visuals + (boosted && !pursuit ? "BoostedSlash" : "MeleeSlash"), playerManager.transform.position, direction,
                Vector3.one * AbsolMoveRules.BasicRange, 0.2f);
            if (!pursuit) charge = (charge + 1) % 3;
            Synchronize();
            if (bonusDelay > 0)
            {
                yield return AttackPause(bonusDelay);
                if (AttackInterrupted) yield break;
                Physics.SyncTransforms();
                // Isolation is decided on the main hit; the delayed bonus must
                // still contact that same living target, never retarget globally.
                if (AbsolMoveRules.CanHit(playerManager, isolated)
                    && CombatTargeting.SectorTargets(playerManager.transform.position, direction, AbsolMoveRules.BasicRange, AbsolMoveRules.BasicAngle).Contains(isolated))
                    isolated.ResolveDamage(AbsolMoveRules.BoostedBonusDamage(playerManager.NetworkObjectId));
            }
            yield return AttackPause(recovery);
        }
        finally
        {
            attackRoutine = null;
            if (playerManager != null && playerManager.IsSpawned) playerManager.MovesController.PresentServerAttackRPC(false, Vector3.zero,
                playerManager.Pokemon.CurrentHp > 0 && !playerManager.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions) ? AbsolMoveRules.Idle : "", 0, playerManager.Pokemon.CurrentHp <= 0,
                AbsolMoveRules.RecoveryAnimationBlend);
        }
    }

    private bool AttackInterrupted => interrupted || playerManager == null || playerManager.Pokemon.CurrentHp <= 0
        || playerManager.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions);

    private IEnumerator AttackPause(float duration)
    {
        float until = Time.time + Mathf.Max(0, duration);
        while (Time.time < until)
        {
            if (AttackInterrupted) { interrupted = true; yield break; }
            yield return null;
        }
    }

    public override void Update()
    {
        range = PursuitReady ? AbsolMoveRules.PursuitRange : AbsolMoveRules.BasicRange;
        base.Update();
        var visible = new HashSet<Pokemon>();
        if (PursuitReady)
            foreach (var target in CombatTargeting.SphereTargets(playerManager.transform.position + Vector3.up * 0.8f, AbsolMoveRules.PursuitRange))
                if (AbsolMoveRules.CanHit(playerManager, target) && Aim.Instance.CanPokemonBeTargeted(target.gameObject, AimTarget.NonAlly, playerManager.CurrentTeam, false))
                {
                    visible.Add(target);
                    if (backs.ContainsKey(target)) continue;
                    var prefab = Resources.Load<GameObject>(AbsolMoveRules.Visuals + "BackHighlight");
                    if (prefab != null) backs[target] = Object.Instantiate(prefab, target.transform);
                }
        foreach (var target in new List<Pokemon>(backs.Keys))
            if (!visible.Contains(target)) { Object.Destroy(backs[target]); backs.Remove(target); }
    }

    public override void ResetAttack()
    {
        if (attackRoutine != null && playerManager != null) playerManager.StopCoroutine(attackRoutine);
        attackRoutine = null; charge = 0; pursuitUntil = 0;
        foreach (var view in backs.Values) Object.Destroy(view); backs.Clear();
        if (playerManager != null && playerManager.IsServer && playerManager.IsSpawned)
        {
            playerManager.MovesController.PresentServerAttackRPC(false, Vector3.zero, "", 0, playerManager.Pokemon.CurrentHp <= 0);
            Synchronize();
        }
        base.ResetAttack();
    }
    public override void Dispose() { ResetAttack(); base.Dispose(); }
}
