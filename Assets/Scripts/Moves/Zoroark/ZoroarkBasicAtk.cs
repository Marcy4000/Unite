using System.Collections;
using UnityEngine;

public class ZoroarkBasicAtk : ChargedBasicAttackBase
{
    private int charge;
    private Coroutine attack;
    private bool disposed;
    protected override int AttackCharge => charge;
    public override bool ServerResolved => true;
    public override float MoveInputBufferDuration => ZoroarkMoveRules.InputBuffer;
    private ZoroarkCharacterRuntime Character => playerManager?.MovesController.Combat?.Character as ZoroarkCharacterRuntime;
    private bool Unique => playerManager != null && (playerManager.IsServer
        ? Character?.ZoroarkNight != null && Character.ZoroarkNight.UniqueReady
        : Character?.ZoroarkUniqueReady == true);

    public override void Initialize(PlayerManager manager) { range = ZoroarkMoveRules.BasicRange; base.Initialize(manager); }
    public override void InitializeServer(PlayerManager manager) { base.InitializeServer(manager); range = ZoroarkMoveRules.BasicRange; }
    public override void ApplyServerState(int value, float until) { charge = value; }
    public override void Update() { range = Unique ? ZoroarkMoveRules.UniqueRange : ZoroarkMoveRules.BasicRange; base.Update(); }
    public override void Perform(bool wildPriority)
    {
        range = Unique ? ZoroarkMoveRules.UniqueRange : ZoroarkMoveRules.BasicRange;
        var target = playerManager.Aim.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target == null && (Unique || playerManager.PlayerMovement.IsMoving)) return;
        playerManager.MovesController.RequestServerBasicAttack(target != null ? target.GetComponent<Pokemon>().NetworkObjectId : ulong.MaxValue);
    }
    public override void PerformOnServer(Pokemon target)
    {
        if (attack != null || disposed) return;
        float reach = Unique ? ZoroarkMoveRules.UniqueRange : ZoroarkMoveRules.BasicRange;
        if (target != null && (!ZoroarkMoveRules.CanHit(playerManager, target)
            || !CombatTargeting.WithinMeleeRange(playerManager.transform.position, target.transform.position, reach))) return;
        if (Unique && target == null) return;
        attack = playerManager.StartCoroutine(Execute(target));
    }
    private IEnumerator Execute(Pokemon primary)
    {
        bool unique = Unique, boosted = unique || charge == 2;
        var night = unique ? Character?.ZoroarkNight : null;
        float scale = Mathf.Clamp(playerManager.MovesController.GetAtkSpeedCooldown() / 0.93333f, 0.2f, 1);
        float windup = ZoroarkMoveRules.BasicWindup * scale, recovery = ZoroarkMoveRules.BasicRecovery * scale;
        Vector3 direction = primary != null ? primary.transform.position - playerManager.transform.position : playerManager.transform.forward;
        direction.y = 0; direction = direction.sqrMagnitude > 0.001f ? direction.normalized : playerManager.transform.forward;
        string clip = unique ? "spell2a2" : boosted ? "atk3" : charge == 0 ? "atk1" : "atk2";
        playerManager.MovesController.PresentServerAttackRPC(true, direction, ZoroarkMoveRules.Animation(playerManager, clip), windup + recovery, blendDuration: 0.04f);
        try
        {
            yield return Pause(windup);
            if (Interrupted) yield break;
            Physics.SyncTransforms();
            bool hit = false;
            foreach (var target in CombatTargeting.SectorTargets(playerManager.transform.position, direction,
                unique ? ZoroarkMoveRules.UniqueRange : ZoroarkMoveRules.BasicRange, ZoroarkMoveRules.BasicAngle))
                if (ZoroarkMoveRules.CanHit(playerManager, target)
                    && target.ResolveDamage(ZoroarkMoveRules.Basic(playerManager.NetworkObjectId, boosted)) > 0) hit = true;
            charge = unique ? 0 : (charge + 1) % 3;
            playerManager.MovesController.SynchronizeBasicAttackRPC(charge, 0);
            if (night != null) night.UniqueAttackLanded(hit);
            playerManager.MovesController.ShowCombatVisualRPC(ZoroarkMoveRules.Visuals + (boosted ? "BoostedSlash" : "MeleeSlash"),
                playerManager.transform.position, direction, Vector3.one * (unique ? ZoroarkMoveRules.UniqueRange : ZoroarkMoveRules.BasicRange), 0.18f);
            yield return Pause(recovery);
        }
        finally
        {
            attack = null;
            if (playerManager != null && playerManager.MovesController != null && playerManager.MovesController.IsSpawned)
                playerManager.MovesController.PresentServerAttackRPC(false, Vector3.zero,
                    !Interrupted ? ZoroarkMoveRules.Animation(playerManager, "idle") : "", 0, playerManager.Pokemon.CurrentHp <= 0, 0.06f);
        }
    }
    private bool Interrupted => disposed || playerManager == null || playerManager.Pokemon.CurrentHp <= 0
        || playerManager.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions);
    private IEnumerator Pause(float duration)
    {
        float until = Time.time + duration;
        while (Time.time < until && !Interrupted) yield return null;
    }
    public override void ResetAttack()
    {
        if (attack != null && playerManager != null) playerManager.StopCoroutine(attack);
        attack = null; charge = 0;
        if (playerManager != null && playerManager.IsServer && playerManager.MovesController != null
            && playerManager.MovesController.IsSpawned)
        {
            playerManager.MovesController.SynchronizeBasicAttackRPC(0, 0);
            playerManager.MovesController.PresentServerAttackRPC(false, Vector3.zero, "", 0, true);
        }
        base.ResetAttack();
    }
    public override void Dispose() { disposed = true; ResetAttack(); base.Dispose(); }
}
