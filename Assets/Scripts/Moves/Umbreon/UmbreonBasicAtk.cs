using System.Collections;
using UnityEngine;

public class UmbreonBasicAtk : ChargedBasicAttackBase
{
    private int charge;
    private Coroutine routine;
    private bool disposed;
    protected override int ReadyCharge => 2;
    protected override int AttackCharge => playerManager != null && playerManager.MovesController.UmbreonMeanBoosted ? 2 : charge;
    public override bool ServerResolved => true;
    public override float MoveInputBufferDuration => 0.15f;
    public override void Initialize(PlayerManager manager) { range = UmbreonMoveRules.BasicRange; base.Initialize(manager); }
    public override void InitializeServer(PlayerManager manager) { base.InitializeServer(manager); range = UmbreonMoveRules.BasicRange; }
    public override void ApplyServerState(int value, float until) { charge = value; }
    public override void Perform(bool wildPriority)
    {
        var target = ActorAim.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target != null) playerManager.MovesController.RequestServerBasicAttack(target.GetComponent<Pokemon>().NetworkObjectId);
    }
    public override void PerformOnServer(Pokemon target)
    {
        if (disposed || routine != null || !UmbreonMoveRules.Enemy(playerManager, target)
            || !UmbreonMoveRules.InCircle(playerManager.transform.position, target.transform.position, range + 0.5f)) return;
        routine = playerManager.StartCoroutine(Attack(target));
    }
    private bool Interrupted => disposed || playerManager == null || !UmbreonMoveRules.Living(playerManager.Pokemon)
        || playerManager.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions);
    private IEnumerator Pause(float duration) { float end = Time.time + duration; while (!Interrupted && Time.time < end) yield return null; }
    private IEnumerator Attack(Pokemon target)
    {
        var actor = playerManager; var combat = actor.MovesController.UmbreonCombat;
        bool mean = combat.InMeanZone, boosted = mean || combat.Charge == 2;
        Vector3 facing = UmbreonMoveRules.Flat(target.transform.position - actor.transform.position).normalized;
        float scale = Mathf.Clamp(actor.MovesController.GetAtkSpeedCooldown() / 0.93333f, 0.2f, 1);
        float windup = UmbreonMoveRules.BasicWindup * scale, recovery = UmbreonMoveRules.BasicRecovery * scale;
        string clip = actor.Pokemon.CurrentLevel >= 3 ? boosted ? "atk3" : "atk" + (combat.Charge % 2 + 1) : "atk" + (boosted ? 6 : combat.Charge + 4);
        actor.MovesController.PresentServerAttackRPC(true, facing, UmbreonMoveRules.Animation(actor, clip), windup + recovery);
        try
        {
            yield return Pause(windup);
            if (Interrupted) yield break;
            combat.SpendAttack(); // Before damage: a marked hit may ready the NEXT attack.
            bool hit = false;
            Vector3 center = actor.transform.position + facing * Mathf.Min(range, 1.5f) + Vector3.up * 0.8f;
            Physics.SyncTransforms();
            foreach (var enemy in CombatTargeting.SphereTargets(center, UmbreonMoveRules.BasicRadius))
            {
                if (!UmbreonMoveRules.Enemy(actor, enemy) || !UmbreonMoveRules.InCircle(actor.transform.position, enemy.transform.position, range + 0.5f)) continue;
                hit |= enemy.ResolveDamage(UmbreonMoveRules.Basic(actor.NetworkObjectId, boosted, mean)) > 0;
            }
            if (boosted && hit) actor.Pokemon.HealDamageRPC(Mathf.FloorToInt(actor.Pokemon.GetMissingHp() * 0.1f), actor.NetworkObjectId);
            actor.MovesController.ShowCombatVisualRPC(UmbreonMoveRules.Visuals + "Claw", center, facing, Vector3.one * UmbreonMoveRules.BasicRadius, 0.2f);
            yield return Pause(recovery);
        }
        finally
        {
            routine = null;
            if (actor != null && actor.IsSpawned) actor.MovesController.PresentServerAttackRPC(false, Vector3.zero,
                !Interrupted ? UmbreonMoveRules.Animation(actor, "idle") : "", 0, actor.Pokemon.CurrentHp <= 0);
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
