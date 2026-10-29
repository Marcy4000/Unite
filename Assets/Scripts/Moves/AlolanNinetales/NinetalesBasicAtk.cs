using System.Collections;
using UnityEngine;

public class NinetalesBasicAtk : ChargedBasicAttackBase
{
    private int charge;
    private Coroutine routine;
    protected override int AttackCharge => playerManager != null && playerManager.MovesController.NinetalesInsideVeil ? 2 : charge;
    public override bool ServerResolved => true;
    public override float MoveInputBufferDuration => NinetalesMoveRules.BasicInputBuffer;
    public int Charge => charge;
    public override void Initialize(PlayerManager manager) { range = NinetalesMoveRules.BasicRange; base.Initialize(manager); }
    public override void InitializeServer(PlayerManager manager) { base.InitializeServer(manager); range = NinetalesMoveRules.BasicRange; }
    public override void ApplyServerState(int value, float until) { charge = value; }
    public override void Perform(bool wildPriority)
    {
        var target = ActorAim.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target != null) playerManager.MovesController.RequestServerBasicAttack(target.GetComponent<Pokemon>().NetworkObjectId);
    }
    public override void PerformOnServer(Pokemon target)
    {
        if (routine != null || !NinetalesMoveRules.CanHit(playerManager, target)
            || (target.transform.position - playerManager.transform.position).sqrMagnitude > range * range) return;
        routine = playerManager.StartCoroutine(Attack(target));
    }
    private bool Interrupted => playerManager == null || !playerManager.IsSpawned || playerManager.Pokemon.CurrentHp <= 0
        || playerManager.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions);
    private IEnumerator Attack(Pokemon target)
    {
        bool boosted = charge == 2 || playerManager.MovesController.NinetalesInsideVeil;
        bool evolved = playerManager.Pokemon.CurrentLevel >= 3;
        string animation = NinetalesMoveRules.Animation(boosted ? evolved ? "atk3" : "atk2" : "atk1", evolved);
        Vector3 direction = target.transform.position - playerManager.transform.position; direction.y = 0;
        float scale = NinetalesMoveRules.BasicTimingScale(playerManager.MovesController.GetAtkSpeedCooldown());
        float windup = NinetalesMoveRules.BasicWindup * scale, recovery = NinetalesMoveRules.BasicRecovery * scale;
        playerManager.MovesController.PresentServerAttackRPC(true, direction, animation, windup + recovery, blendDuration: NinetalesMoveRules.AnimationBlend);
        try
        {
            float until = Time.time + windup;
            while (Time.time < until) { if (Interrupted) yield break; yield return null; }
            if (Interrupted || !NinetalesMoveRules.CanHit(playerManager, target)) yield break;
            playerManager.MovesController.LaunchNinetalesBasic(target, boosted);
            charge = boosted ? 0 : (charge + 1) % 3;
            playerManager.MovesController.SynchronizeBasicAttackRPC(charge, 0);
            until = Time.time + recovery;
            while (Time.time < until) { if (Interrupted) yield break; yield return null; }
        }
        finally
        {
            routine = null;
            if (playerManager != null && playerManager.IsSpawned)
                playerManager.MovesController.PresentServerAttackRPC(false, Vector3.zero,
                    !Interrupted ? NinetalesMoveRules.Animation("idle", evolved) : "", 0, playerManager.Pokemon.CurrentHp <= 0, NinetalesMoveRules.AnimationBlend);
        }
    }
    public override void ResetAttack()
    {
        if (routine != null && playerManager != null) playerManager.StopCoroutine(routine);
        routine = null; charge = 0;
        if (playerManager != null && playerManager.IsServer && playerManager.IsSpawned && playerManager.MovesController.IsSpawned)
        {
            playerManager.MovesController.SynchronizeBasicAttackRPC(0, 0);
            playerManager.MovesController.PresentServerAttackRPC(false, Vector3.zero, "", 0, playerManager.Pokemon.CurrentHp <= 0);
        }
        base.ResetAttack();
    }
    public override void Dispose() { ResetAttack(); base.Dispose(); }
}
