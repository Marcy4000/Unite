using System.Collections;
using UnityEngine;

public class EspeonBasicAtk : ChargedBasicAttackBase
{
    private int charge;
    private Coroutine routine;
    private bool disposed;
    private HPBar boostBar;
    private bool Evolved => playerManager != null && playerManager.Pokemon.CurrentLevel >= 3;
    protected override int ReadyCharge => Evolved ? 1 : 2;
    protected override int AttackCharge => Evolved ? playerManager.MovesController.EspeonBoostReady ? 1 : 0 : charge;
    protected override bool ShowAttackCounter => !Evolved;
    public override bool ServerResolved => true;
    public override float MoveInputBufferDuration => EspeonMoveRules.InputBuffer;
    public override void Initialize(PlayerManager manager)
    {
        HideBoostGauge();
        range = EspeonMoveRules.BasicRange; base.Initialize(manager);
        UpdateBoostGauge();
    }
    public override void InitializeServer(PlayerManager manager) { base.InitializeServer(manager); range = EspeonMoveRules.BasicRange; }
    public override void ApplyServerState(int value, float until) { charge = value; }
    public override void Update() { base.Update(); UpdateBoostGauge(); }
    private void UpdateBoostGauge()
    {
        if (playerManager == null || (playerManager.IsSpawned && !playerManager.IsOwner))
        { HideBoostGauge(); return; }
        if (boostBar != playerManager.HPBar) { HideBoostGauge(); boostBar = playerManager.HPBar; }
        if (boostBar == null) return;
        bool show = Evolved && playerManager.Pokemon.CurrentHp > 0;
        boostBar.UpdateGenericGuageValue(show ? playerManager.MovesController.EspeonBoostProgress : 0);
        boostBar.ShowGenericGuage(show);
    }
    private void HideBoostGauge()
    {
        if (boostBar != null) { boostBar.UpdateGenericGuageValue(0); boostBar.ShowGenericGuage(false); }
        boostBar = null;
    }
    public override void Perform(bool wildPriority)
    {
        var marked = playerManager.MovesController.EspeonMarkedTarget;
        var target = EspeonMoveRules.CanHit(playerManager, marked) && Vector3.Distance(marked.transform.position, playerManager.transform.position) <= range
            ? marked.gameObject : ActorAim.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (target != null) playerManager.MovesController.RequestServerBasicAttack(target.GetComponent<Pokemon>().NetworkObjectId);
    }
    public override void PerformOnServer(Pokemon target)
    {
        if (disposed || routine != null || !EspeonMoveRules.CanHit(playerManager, target)
            || Vector3.Distance(target.transform.position, playerManager.transform.position) > range + 0.5f) return;
        routine = playerManager.StartCoroutine(Attack(target));
    }
    private bool Interrupted => disposed || playerManager == null || !EspeonMoveRules.Living(playerManager.Pokemon)
        || playerManager.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions);
    private IEnumerator Pause(float duration) { float end = Time.time + duration; while (!Interrupted && Time.time < end) yield return null; }
    private IEnumerator Attack(Pokemon target)
    {
        bool evolved = Evolved, boosted = evolved ? playerManager.MovesController.EspeonBoostReady : charge == 2;
        var actor = playerManager;
        float speedScale = Mathf.Clamp(actor.MovesController.GetAtkSpeedCooldown() / 0.93333f, 0.2f, 1);
        float windup = EspeonMoveRules.BasicWindup * speedScale, recovery = EspeonMoveRules.BasicRecovery * speedScale;
        Vector3 direction = EspeonMoveRules.Flat(target.transform.position - actor.transform.position).normalized;
        string clip = evolved ? boosted ? "atk3" : "atk1" : "atk" + (charge + 4);
        actor.MovesController.PresentServerAttackRPC(true, direction, EspeonMoveRules.Animation(actor, clip), windup + recovery);
        try
        {
            yield return Pause(windup);
            if (Interrupted || !EspeonMoveRules.CanHit(actor, target)) yield break;
            if (evolved && boosted)
            {
                actor.MovesController.SetEspeonBoostReady(actor.NetworkManager.ServerTime.Time + EspeonMoveRules.BoostRecharge);
                Vector3 origin = actor.transform.position + Vector3.up * 0.8f;
                actor.MovesController.ShowCombatVisualRPC(EspeonMoveRules.Visuals + "Beam", origin, direction,
                    new Vector3(EspeonMoveRules.BeamWidth, EspeonMoveRules.BeamWidth, EspeonMoveRules.BeamLength), 0.3f);
                Physics.SyncTransforms();
                foreach (var enemy in CombatTargeting.SweptSphereTargets(origin, origin + direction * EspeonMoveRules.BeamLength, EspeonMoveRules.BeamWidth * 0.5f))
                    if (actor.MovesController.EspeonCombat.Hit(enemy, EspeonMoveRules.Basic(actor.NetworkObjectId, true, true), true) > 0)
                    { actor.MovesController.EspeonCombat.Slow(enemy, 30, 1); actor.MovesController.EspeonCombat.AddBlasts(1); }
            }
            else actor.MovesController.LaunchEspeonBasic(target, boosted);
            charge = evolved ? 0 : (charge + 1) % 3;
            actor.MovesController.SynchronizeBasicAttackRPC(charge, 0);
            yield return Pause(recovery);
        }
        finally
        {
            routine = null;
            if (actor != null && actor.IsSpawned) actor.MovesController.PresentServerAttackRPC(false, Vector3.zero,
                !Interrupted ? EspeonMoveRules.Animation(actor, "idle") : "", 0, actor.Pokemon.CurrentHp <= 0);
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
    public override void Dispose() { disposed = true; ResetAttack(); HideBoostGauge(); base.Dispose(); }
}
