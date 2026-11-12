using System.Collections;
using UnityEngine;

public class LucarioBasicAtk : ChargedBasicAttackBase
{
    private Coroutine routine;
    private bool disposed;
    protected override int AttackCharge => playerManager != null ? playerManager.MovesController.LucarioCharge : 0;
    public override bool ServerResolved => true;
    public override float MoveInputBufferDuration => 0.15f;
    public override void Initialize(PlayerManager manager) { base.Initialize(manager); UpdateRange(); }
    public override void InitializeServer(PlayerManager manager) { base.InitializeServer(manager); UpdateRange(); }
    public override void ApplyServerState(int value, float until) { UpdateRange(); }
    private void UpdateRange()
    {
        if (playerManager == null) return;
        range = playerManager.MovesController.LucarioSpinRemaining > 0 ? LucarioMoveRules.ExtremeAttackRadius
            : playerManager.MovesController.LucarioKickRemaining > 0 ? LucarioMoveRules.KickRange
            : AttackCharge == 2 ? LucarioMoveRules.BoostedRange : LucarioMoveRules.BasicRange;
    }
    public override void Update() { UpdateRange(); base.Update(); }
    public override void Perform(bool wildPriority)
    {
        UpdateRange(); var target = ActorAim.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target != null) playerManager.MovesController.RequestServerBasicAttack(target.GetComponent<Pokemon>().NetworkObjectId);
    }
    public override void PerformOnServer(Pokemon target)
    {
        UpdateRange();
        if (disposed || routine != null || !LucarioMoveRules.Enemy(playerManager, target)
            || !CombatTargeting.WithinMeleeRange(playerManager.transform.position, target.transform.position, range)) return;
        routine = playerManager.StartCoroutine(Attack(target));
    }
    private bool Interrupted => disposed || playerManager == null || !LucarioMoveRules.Living(playerManager.Pokemon)
        || playerManager.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions);
    private IEnumerator Pause(float time) { double until = playerManager.NetworkManager.ServerTime.Time + time; while (!Interrupted && playerManager.NetworkManager.ServerTime.Time < until) yield return null; }
    private IEnumerator Attack(Pokemon primary)
    {
        var actor = playerManager; var combat = actor.MovesController.LucarioCombat;
        bool spin = combat.SpinReady, kick = !spin && combat.KickReady, special = spin || kick, boosted = !special && combat.Charge == 2;
        float scale = Mathf.Clamp(actor.MovesController.GetAtkSpeedCooldown() / 0.93333f, 0.2f, 1);
        float windup = LucarioMoveRules.BasicWindup * scale, recovery = LucarioMoveRules.BasicRecovery * scale, gap = boosted ? LucarioMoveRules.BoostedGap * scale : 0;
        Vector3 direction = LucarioMoveRules.Flat(primary.transform.position - actor.transform.position).normalized;
        string clip = spin ? "spell12" : kick ? "atk3" : boosted ? "atk4" : combat.Charge == 0 ? "atk1" : "atk2";
        actor.MovesController.PresentServerAttackRPC(true, direction, LucarioMoveRules.Animation(clip), windup + recovery + gap + (kick ? LucarioMoveRules.KickTravel : 0), blendDuration: 0.06f);
        if (boosted) actor.MovesController.ShowLucarioAttachedVisualRPC("Bone", "Bip001 R Hand", windup + gap + recovery);
        try
        {
            yield return Pause(windup); if (Interrupted) yield break;
            if (special) combat.SpendSpecial(spin); else combat.SpendBasic();
            if (kick)
            {
                if (!LucarioMoveRules.Enemy(actor, primary)) yield break;
                Vector3 destination = actor.PlayerMovement.GetWallCrossingDestination(primary.transform.position - direction * 0.7f);
                actor.MovesController.DriveBasicAttackRPC(actor.transform.position, destination, LucarioMoveRules.KickTravel);
                yield return Pause(LucarioMoveRules.KickTravel); if (Interrupted) yield break;
            }
            Physics.SyncTransforms();
            if (spin)
            {
                foreach (var target in CombatTargeting.SphereTargets(actor.transform.position + Vector3.up, LucarioMoveRules.ExtremeAttackRadius))
                    if (LucarioMoveRules.Enemy(actor, target)) target.ResolveDamage(LucarioMoveRules.Basic(actor.NetworkObjectId, 0.75f));
            }
            else
            {
                foreach (var target in CombatTargeting.BoxTargets(actor.transform.position + direction * 1.625f + Vector3.up,
                    new Vector3(LucarioMoveRules.BasicWidth, 2.5f, LucarioMoveRules.BasicLength) * 0.5f, Quaternion.LookRotation(direction)))
                    if (LucarioMoveRules.Enemy(actor, target)) target.ResolveDamage(LucarioMoveRules.Basic(actor.NetworkObjectId));
            }
            if (boosted)
            {
                yield return Pause(gap); if (Interrupted) yield break; Physics.SyncTransforms();
                ushort id = LucarioEffectIds.Acquire();
                foreach (var target in CombatTargeting.SphereTargets(actor.transform.position + direction * 1.5f + Vector3.up, LucarioMoveRules.BoostedRadius))
                {
                    if (!LucarioMoveRules.Enemy(actor, target) || target.ResolveDamage(LucarioMoveRules.Basic(actor.NetworkObjectId, 0.3f, true)) <= 0) continue;
                    target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.5f, true, id).WithSource(actor.NetworkObjectId));
                    if (target.HasStatusEffect(id)) target.ApplyKnockupRPC(1, 0.5f);
                }
                LucarioEffectIds.Release(id, 0.5f);
            }
            actor.MovesController.ShowCombatVisualRPC(LucarioMoveRules.Visuals + (spin || boosted ? "Spin" : "Impact"), actor.transform.position, direction, Vector3.one * (spin ? LucarioMoveRules.ExtremeAttackRadius : 1), 0.2f);
            yield return Pause(recovery);
        }
        finally
        {
            routine = null;
            if (actor != null && actor.IsSpawned) actor.MovesController.PresentServerAttackRPC(false, Vector3.zero, !Interrupted ? LucarioMoveRules.Animation("idle") : "", 0, actor.Pokemon.CurrentHp <= 0, 0.08f);
        }
    }
    public override void ResetAttack()
    {
        if (routine != null && playerManager != null) playerManager.StopCoroutine(routine); routine = null;
        if (playerManager != null && playerManager.IsServer && playerManager.IsSpawned) playerManager.MovesController.PresentServerAttackRPC(false, Vector3.zero, "", 0, true);
        base.ResetAttack();
    }
    public override void Dispose() { disposed = true; ResetAttack(); base.Dispose(); }
}
