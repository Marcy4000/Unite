using Unity.Netcode;
using UnityEngine;

public class MarshadowAuraSphere : MarshadowMoveBase
{
    private enum Phase { Idle, Casting, Projectile, Marked, Lunging, Recovery }
    private Phase phase;
    private Pokemon target;
    private float deadline;
    private int markEffect;
    private readonly WallCrossingTraversal traversal = new WallCrossingTraversal();
    public int LastCast => cast;
    protected override float AimRange => MarshadowMoveRules.AuraRange;
    protected override bool RequiresMovement => phase == Phase.Lunging;
    public MarshadowAuraSphere() { Name = "Aura Sphere"; Cooldown = 9f; }

    public override void Start(PlayerManager controller)
    {
        if (phase == Phase.Projectile || executing) return;
        if (phase == Phase.Marked)
        {
            if (controller.Pokemon.HasStatusEffect(StatusType.Immobilized)) return;
            if (!ValidTarget() || Time.time >= deadline) { Complete(); return; }
            playerManager = controller; IsActive = true; return;
        }
        base.Start(controller);
    }
    public override void Finish()
    {
        if (!IsActive || executing) return;
        if (phase == Phase.Marked)
        {
            if (!ValidTarget() || Time.time >= deadline) { Complete(); return; }
            direction = MarshadowMoveRules.Direction(target.transform.position - playerManager.transform.position, playerManager.transform.forward);
            Begin(); phase = Phase.Lunging;
            traversal.Begin(playerManager.PlayerMovement);
            MarshadowMoveRuntime.ShowWindow(playerManager, 0, 0);
            MarshadowMoveRuntime.EndEffect(playerManager, ref markEffect);
            effect = MarshadowMoveRuntime.Effect(playerManager, MarshadowEffect.Rush, 0.45f, stage: 2);
        }
        else if (phase == Phase.Idle)
        {
            Begin(); phase = Phase.Casting;
            MarshadowMoveRuntime.PlayAction(playerManager, "pm0883_ba21_tokusyu01", 1.733f);
        }
    }
    public override void Update()
    {
        base.Update();
        if ((phase == Phase.Marked && (!ValidTarget() || Time.time >= deadline)) || (phase == Phase.Projectile && Time.time >= deadline)) Complete();
    }
    protected override void Advance()
    {
        switch (phase)
        {
            case Phase.Casting:
                if (elapsed < 0.24f) return;
                phase = Phase.Projectile; executing = false;
                ActionLock.Release(); MarshadowMoveRuntime.ReturnToLocomotion(playerManager);
                deadline = Time.time + 3f;
                playerManager.MovesController.LaunchMarshadowAuraRPC(cast, playerManager.transform.position, direction);
                break;
            case Phase.Lunging:
                if (!ValidTarget()) { Complete(); return; }
                Vector3 offset = target.transform.position - playerManager.transform.position; offset.y = 0;
                bool safeLanding = playerManager.PlayerMovement.IsWallCrossingLandingValid(playerManager.transform.position);
                if (offset.magnitude <= 1f && safeLanding)
                {
                    traversal.End();
                    Hit(MarshadowHit.AuraFollowup, target: target.NetworkObjectId);
                    // A host-side KO can synchronously invoke OnTargetDeath.
                    // Do not resurrect the completed sequence after its cleanup.
                    if (phase == Phase.Lunging) { phase = Phase.Recovery; elapsed = 0; }
                    return;
                }
                direction = offset.normalized;
                playerManager.transform.rotation = Quaternion.LookRotation(direction);
                // The usual melee stopping distance may still be inside the
                // wall. Continue toward the target until there is room to land.
                float requested = Mathf.Min(Mathf.Max(0, offset.magnitude - (safeLanding ? 0.8f : 0)), 19f * Time.deltaTime);
                Vector3 movement = traversal.Move(direction * requested);
                if (elapsed >= 0.6f || movement.sqrMagnitude < requested * requested * 0.01f) Complete();
                break;
            case Phase.Recovery:
                if (elapsed >= 0.15f) Complete();
                break;
        }
    }
    public void ProjectileResult(int id, bool hit, ulong targetId)
    {
        if (id != cast || phase != Phase.Projectile || playerManager.Pokemon.CurrentHp <= 0) return;
        if (!hit || !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)
            || !obj.TryGetComponent(out target) || !ValidTarget()) { Complete(); return; }
        phase = Phase.Marked; deadline = Time.time + MarshadowMoveRules.AuraWindow;
        target.OnDeath += OnTargetDeath;
        markEffect = MarshadowMoveRuntime.Effect(playerManager, MarshadowEffect.AuraMark, MarshadowMoveRules.AuraWindow, targetId);
        MarshadowMoveRuntime.ShowWindow(playerManager, 0, MarshadowMoveRules.AuraWindow);
    }
    private bool ValidTarget() => MarshadowMoveRuntime.CanHit(playerManager, target)
        && (target.transform.position - playerManager.transform.position).sqrMagnitude <= 100f;
    private void OnTargetDeath(DamageInfo _) { Complete(); }
    protected override void Complete()
    {
        traversal.End();
        if (target != null) target.OnDeath -= OnTargetDeath;
        target = null; phase = Phase.Idle;
        MarshadowMoveRuntime.ShowWindow(playerManager, 0, 0);
        MarshadowMoveRuntime.EndEffect(playerManager, ref markEffect);
        // Also invalidates a pending local projectile presentation; server-side
        // projectile lifetime is independently cancelled by caster death.
        if (playerManager != null && playerManager.IsSpawned && cast != 0) playerManager.MovesController.CancelMarshadowCastRPC(cast);
        base.Complete();
    }
}
