using System.Collections.Generic;
using UnityEngine;

public class MarshadowUnite : MarshadowMoveBase
{
    private MoveAsset borrowed;
    public MarshadowUnite() { Name = "Mirror Shadow"; Cooldown = 0; }
    public override void Start(PlayerManager controller)
    {
        if (executing) return;
        // Mirror Shadow has no directional cast and does not touch shared aim.
        playerManager = controller; IsActive = true; wasMoveSuccessful = false;
        direction = controller.transform.forward;
    }
    public override void Finish()
    {
        if (!IsActive || executing) return;
        var candidates = new List<MoveAsset>();
        if (GameManager.Instance != null)
            foreach (PlayerManager enemy in GameManager.Instance.Players)
            {
                if (enemy == null || enemy == playerManager || enemy.CurrentTeam.IsOnSameTeam(playerManager.CurrentTeam)) continue;
                MoveAsset source = enemy.Pokemon.BaseStats.GetUniteMove();
                if (source != null && MarshadowCopyRules.Supports(source.move)) candidates.Add(source);
            }
        if (candidates.Count == 0) { Cancel(); return; }
        borrowed = candidates[Random.Range(0, candidates.Count)];
        Begin();
        MarshadowMoveRuntime.PlayAction(playerManager, "pm0883_ba21_tokusyu02");
        effect = MarshadowMoveRuntime.Effect(playerManager, MarshadowEffect.MirrorShadow, 2f);
        // Pay once at commitment; replacing the slot after the channel is not
        // another activation and must not spend or refill Mirror's resource.
        playerManager.MovesController.SpendMarshadowUnite();
    }
    protected override void Advance()
    {
        if (elapsed < 2f) return;
        MoveAsset source = borrowed;
        borrowed = null; committed = false;
        base.Complete();
        playerManager.MovesController.InstallMarshadowCopy(source);
    }
    public override void ResetMove()
    {
        borrowed = null; committed = false; base.ResetMove();
    }
}

// Explicit adapters, not foreign MoveBase instances with missing passives,
// model bones, or basic-attack components. New licenses opt in deliberately.
public static class MarshadowCopyRules
{
    public static bool Supports(AvailableMoves move)
    {
        switch (move)
        {
            case AvailableMoves.BlazingBycicleKick:
            case AvailableMoves.GlaceonUnite:
            case AvailableMoves.SylveonUnite:
            case AvailableMoves.VaporeonUnite:
            case AvailableMoves.EmboarUnite:
            case AvailableMoves.FlareonUnite:
            case AvailableMoves.DuraludonUnite: return true;
            default: return false;
        }
    }
    public static bool Targeted(AvailableMoves move) => move == AvailableMoves.BlazingBycicleKick || move == AvailableMoves.FlareonUnite;
    public static float Radius(AvailableMoves move) => move == AvailableMoves.VaporeonUnite || move == AvailableMoves.DuraludonUnite ? 6f : 5f;
    public static string Description(AvailableMoves move)
    {
        switch (move)
        {
            case AvailableMoves.BlazingBycicleKick: return "Launch a homing physical strike at the selected opponent.";
            case AvailableMoves.GlaceonUnite: return "Leap forward and create a slowing shadow field for 5s. Deals physical damage on landing; does not generate icicles.";
            case AvailableMoves.SylveonUnite: return "Release a shadow burst, then gain 50 percentage points of lifesteal and spell vamp for 6s.";
            case AvailableMoves.VaporeonUnite: return "Release a shadow tempest that damages enemies, reduces their defenses and heals allies. Uses Marshadow's Attack.";
            case AvailableMoves.EmboarUnite: return "Rush forward and slam the ground, launching nearby opponents and slowing enemies at the edge. Gain a shield. Uses Marshadow's Attack; does not copy Reckless.";
            case AvailableMoves.FlareonUnite: return "Rush to the selected opponent and burst, burning nearby enemies and gaining a shield.";
            default: return "Recreate Revolving Ruin's sweeping beam, final shove and burning ring, using Marshadow's Attack.";
        }
    }
}

public class MarshadowCopiedUnite : MarshadowMoveBase
{
    public readonly MoveAsset Source;
    private Pokemon target;
    private bool released;
    private Vector3 start;
    private readonly float expiresAt;
    private bool copyReady;
    private float nextRingbreakerReport;
    protected override float AimRange => MarshadowCopyRules.Targeted(Source.move) ? 8f : MarshadowCopyRules.Radius(Source.move);
    protected override bool RequiresMovement => Source.move == AvailableMoves.GlaceonUnite || Source.move == AvailableMoves.FlareonUnite || Source.move == AvailableMoves.EmboarUnite;
    public MarshadowCopiedUnite(MoveAsset source)
    {
        Source = source; Name = "Shadow Copy: " + source.moveName; Cooldown = 0; expiresAt = Time.time + 15f;
    }
    public override void Start(PlayerManager controller)
    {
        if (executing) return;
        base.Start(controller);
        if (MarshadowCopyRules.Targeted(Source.move))
        {
            ActorAim.HideSkillshotAim(); ActorAim.InitializeAutoAim(AimRange, 90f, AimTarget.NonAlly);
            target = ActorAim.SureHitAim()?.GetComponent<Pokemon>();
        }
    }
    public override void Update()
    {
        if (!executing && Time.time >= expiresAt) { Expire(); return; }
        base.Update();
        if (IsActive && MarshadowCopyRules.Targeted(Source.move)) target = ActorAim.SureHitAim()?.GetComponent<Pokemon>();
    }
    public override void Finish()
    {
        if (!IsActive || executing) return;
        if (MarshadowCopyRules.Targeted(Source.move) && !MarshadowMoveRuntime.CanHit(playerManager, target)) { Cancel(); return; }
        ActorAim.HideAutoAim();
        Begin(); released = false; start = playerManager.transform.position;
        playerManager.MovesController.SpendMarshadowUnite();
        MarshadowMoveRuntime.PlayAction(playerManager, "pm0883_ba21_tokusyu02");
        effect = MarshadowMoveRuntime.Effect(playerManager, MarshadowEffect.MirrorShadow, 0.5f);
        if (Source.move == AvailableMoves.DuraludonUnite || Source.move == AvailableMoves.EmboarUnite)
        {
            released = true;
            playerManager.MovesController.CastMarshadowCopyRPC(Source.move, cast, start, direction, 0);
        }
    }
    protected override void Advance()
    {
        if ((Source.move == AvailableMoves.DuraludonUnite || Source.move == AvailableMoves.EmboarUnite) && !copyReady)
        {
            if (elapsed >= 4f)
            {
                playerManager.MovesController.CancelMarshadowCastRPC(cast);
                Complete();
            }
            return;
        }
        if (Source.move == AvailableMoves.EmboarUnite)
        {
            float previous = Mathf.Max(0, elapsed - Time.deltaTime);
            float step = Mathf.Max(0, Mathf.Min(elapsed, 0.5f) - Mathf.Max(previous, 0.2f));
            if (step > 0) playerManager.PlayerMovement.MoveWithWalls(direction * (5 / 0.3f) * step);
            if (elapsed >= nextRingbreakerReport && nextRingbreakerReport < 100)
            {
                bool finished = elapsed >= 0.5f;
                nextRingbreakerReport = finished ? 100 : elapsed + 0.05f;
                playerManager.MovesController.MarshadowRingbreakerMovementRPC(cast, finished, playerManager.transform.position);
            }
            if (elapsed >= 0.78f) Complete();
            return;
        }
        if (Source.move == AvailableMoves.FlareonUnite && !released)
        {
            if (!MarshadowMoveRuntime.CanHit(playerManager, target)) { Complete(); return; }
            Vector3 delta = target.transform.position - playerManager.transform.position; delta.y = 0;
            direction = MarshadowMoveRules.Direction(delta, direction);
            if (elapsed >= 0.2f && delta.magnitude > 1f)
            {
                playerManager.PlayerMovement.MoveWithWalls(direction * Mathf.Min(Mathf.Max(0, delta.magnitude - 0.8f), 20f * Time.deltaTime));
                playerManager.transform.rotation = Quaternion.LookRotation(direction);
            }
            if (delta.magnitude > 1.3f && elapsed < 0.7f) return;
            if (delta.magnitude > 1.3f) { Complete(); return; }
        }
        if (Source.move == AvailableMoves.GlaceonUnite && !released && elapsed < 0.3f)
            playerManager.PlayerMovement.MoveWithWalls(direction * Mathf.Min(3f, 10f * Time.deltaTime));
        if (!released && elapsed >= 0.3f)
        {
            released = true;
            playerManager.MovesController.CastMarshadowCopyRPC(Source.move, cast, playerManager.transform.position, direction, target != null ? target.NetworkObjectId : 0);
        }
        if (elapsed >= (Source.move == AvailableMoves.DuraludonUnite ? 2.1f : 0.55f)) Complete();
    }
    public void CopyReady(int id, bool success)
    {
        if (!executing || id != cast || (Source.move != AvailableMoves.DuraludonUnite && Source.move != AvailableMoves.EmboarUnite)) return;
        if (!success) { Complete(); return; }
        copyReady = true; elapsed = nextRingbreakerReport = 0;
    }
    protected override void Complete()
    {
        target = null;
        // Mirror and the borrowed cast both pay at commitment, not completion.
        committed = false; base.Complete();
        if (playerManager != null) playerManager.MovesController.RestoreMarshadowUnite(this);
    }
    public override void Cancel()
    {
        if (IsActive) ActorAim.HideAutoAim();
        base.Cancel();
    }
    private void Expire()
    {
        playerManager.MovesController.DiscardMarshadowUnite();
        Cancel(); Complete();
    }
    public override void ResetMove()
    {
        // Death discards an unused borrowed use; it cannot refund a full Mirror.
        playerManager.MovesController.DiscardMarshadowUnite();
        base.ResetMove();
    }
}
