using System.Collections;
using UnityEngine;

public class TrevenantBasicAtk : ChargedBasicAttackBase
{
    private int charge;
    private Coroutine routine;
    private bool disposed;
    protected override int ReadyCharge => 2;
    protected override int AttackCharge => charge;
    public override bool ServerResolved => true;
    public override float MoveInputBufferDuration => 0.15f;
    public override void Initialize(PlayerManager manager) { base.Initialize(manager); range = TrevenantMoveRules.BasicRange; }
    public override void InitializeServer(PlayerManager manager) { base.InitializeServer(manager); range = TrevenantMoveRules.BasicRange; }
    public override void ApplyServerState(int value, float until) { charge = value; }
    public override void Perform(bool wildPriority)
    {
        var target = ActorAim.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target != null) playerManager.MovesController.RequestServerBasicAttack(target.GetComponent<Pokemon>().NetworkObjectId);
    }
    public override void PerformOnServer(Pokemon target)
    {
        if (disposed || routine != null || !TrevenantMoveRules.Enemy(playerManager, target)
            || !CombatTargeting.WithinMeleeRange(playerManager.transform.position, target.transform.position, range)) return;
        routine = playerManager.StartCoroutine(Attack(target));
    }
    private bool Interrupted => disposed || playerManager == null || !TrevenantMoveRules.Living(playerManager.Pokemon)
        || playerManager.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions);
    private IEnumerator Pause(float duration) { float until = Time.time + duration; while (!Interrupted && Time.time < until) yield return null; }
    private IEnumerator Attack(Pokemon target)
    {
        var actor = playerManager; var combat = actor.MovesController.TrevenantCombat;
        bool boosted = combat.Charge == 2;
        Vector3 direction = TrevenantMoveRules.Flat(target.transform.position - actor.transform.position).normalized;
        float scale = Mathf.Clamp(actor.MovesController.GetAtkSpeedCooldown() / TrevenantMoveRules.BasicReferenceCooldown, 0.2f, 1);
        float windup = TrevenantMoveRules.BasicWindup * scale, recovery = TrevenantMoveRules.BasicRecovery * scale;
        actor.MovesController.PresentServerAttackRPC(true, direction, TrevenantMoveRules.Animation(actor, "atk" + (combat.Charge + 1)), windup + recovery);
        try
        {
            yield return Pause(windup); if (Interrupted) yield break;
            combat.SpendAttack(); bool empowered = combat.HornAttacks > 0; bool hit = false;
            var damage = new DamageInfo(actor.NetworkObjectId, boosted ? 1.3f : 1, 0, 0, DamageType.Physical, DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit);
            Vector3 center = actor.transform.position + direction * TrevenantMoveRules.BasicOffset + Vector3.up * 0.8f;
            Physics.SyncTransforms();
            foreach (var enemy in CombatTargeting.SphereTargets(center, TrevenantMoveRules.BasicRadius))
            {
                if (!TrevenantMoveRules.Enemy(actor, enemy) || !CombatTargeting.WithinMeleeRange(actor.transform.position, enemy.transform.position, range)) continue;
                if (enemy.ResolveDamage(damage) <= 0) continue;
                hit = true;
                if (boosted) combat.HealFraction(0.05f);
                if (empowered && TrevenantMoveRules.Living(enemy)) enemy.ResolveDamage(TrevenantMoveRules.HornBonus(actor.NetworkObjectId));
            }
            if (hit && empowered) { combat.SpendHornAttack(); actor.MovesController.ReduceTrevenantCooldown(1, TrevenantMoveRules.HornAttackReduction); }
            actor.MovesController.ShowCombatVisualRPC(TrevenantMoveRules.Visuals + "Claw", center, direction, Vector3.one * TrevenantMoveRules.BasicRadius, 0.2f);
            yield return Pause(recovery);
        }
        finally
        {
            routine = null;
            if (actor != null && actor.IsSpawned) actor.MovesController.PresentServerAttackRPC(false, Vector3.zero,
                !Interrupted ? TrevenantMoveRules.Animation(actor, "idle") : "", 0, actor.Pokemon.CurrentHp <= 0);
        }
    }
    public override void ResetAttack()
    {
        if (routine != null && playerManager != null) playerManager.StopCoroutine(routine);
        routine = null; charge = 0;
        if (playerManager != null && playerManager.IsServer && playerManager.IsSpawned)
        {
            playerManager.MovesController.SynchronizeBasicAttackRPC(0, 0);
            playerManager.MovesController.PresentServerAttackRPC(false, Vector3.zero, "", 0, true);
        }
        base.ResetAttack();
    }
    public override void Dispose() { disposed = true; ResetAttack(); base.Dispose(); }
}
