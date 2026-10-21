using System.Collections.Generic;
using UnityEngine;

public class JolteonUnite : MoveBase
{
    public float dashSpeed = JoltMoveRules.BlitzSpeed;
    public float dashDuration = JoltMoveRules.BlitzDuration;
    public float sphereCastRadius = 0.65f;
    public float turningFactor = 2.5f;
    private readonly JoltActionLock actionLock = new JoltActionLock();
    private readonly Dictionary<ulong, float> nextHit = new Dictionary<ulong, float>();
    private Vector3 direction;
    private float remaining;
    private int effect;
    private bool dashing;
    public bool IsDashing => dashing;

    public JolteonUnite() { Name = "Lightning Blitz"; Cooldown = 0; }
    public override void Start(PlayerManager controller)
    {
        if (dashing) return;
        base.Start(controller);
        Aim.Instance.InitializeDashAim(5f);
        direction = Aim.Instance.DashAim();
    }

    public override void Update()
    {
        if (IsActive) direction = Aim.Instance.DashAim();
        if (!dashing) return;
        float delta = Mathf.Min(Time.deltaTime, remaining);
        // Movement input steers on keyboard and controller, preserving the
        // current control scheme and the established wall-bounce behaviour.
        Vector2 input = playerManager.PlayerControls.Movement.Move.ReadValue<Vector2>();
        Vector3 steer = new Vector3(input.x, 0, input.y);
        if (steer.sqrMagnitude > 0.05f)
            direction = Vector3.RotateTowards(direction, steer.normalized, turningFactor * delta, 0).normalized;

        Vector3 from = playerManager.transform.position;
        float distance = dashSpeed * delta;
        RaycastHit nearest = default;
        bool wall = false;
        foreach (var hit in Physics.SphereCastAll(from + Vector3.up, sphereCastRadius, direction, distance + 0.05f))
        {
            if (!hit.collider.CompareTag("Wall") || (wall && hit.distance >= nearest.distance)) continue;
            nearest = hit;
            wall = true;
        }
        if (wall)
        {
            float toWall = Mathf.Max(0, nearest.distance - 0.03f);
            MoveAndHit(direction * Mathf.Min(toWall, distance));
            direction = JoltMoveRules.Reflect(direction, nearest.normal);
            MoveAndHit(direction * Mathf.Max(0, distance - toWall));
        }
        else
        {
            Vector3 moved = MoveAndHit(direction * distance);
            // A walkmesh edge without a Wall collider must not pin the rush.
            if (distance > 0 && moved.sqrMagnitude < distance * distance * 0.01f) direction = -direction;
        }
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        remaining -= delta;
        if (remaining <= 0) EndDash(true);
    }

    private Vector3 MoveAndHit(Vector3 displacement)
    {
        Vector3 from = playerManager.transform.position;
        Vector3 moved = playerManager.PlayerMovement.MoveWithWalls(displacement);
        foreach (Pokemon target in JoltMoveRuntime.Sweep(playerManager, from, playerManager.transform.position, 1f))
        {
            if (nextHit.TryGetValue(target.NetworkObjectId, out float until) && Time.time < until) continue;
            nextHit[target.NetworkObjectId] = Time.time + 0.7f;
            target.TakeDamageRPC(new DamageInfo(playerManager.NetworkObjectId, 1.8f, 6, 300, DamageType.Physical, DamageProprieties.IsUniteMove));
            JoltMoveRuntime.RefreshStat(target, new StatChange(20, Stat.Speed, 2f, true, false, true, JoltMoveRules.BlitzSlow));
        }
        return moved;
    }

    public override void Finish()
    {
        if (!IsActive || dashing) return;
        Aim.Instance.HideDashAim();
        if (direction.sqrMagnitude > 0.001f)
        {
            direction.Normalize();
            remaining = dashDuration;
            nextHit.Clear();
            dashing = true;
            actionLock.Acquire(playerManager);
            playerManager.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, dashDuration, true, JoltMoveRules.BlitzUnstoppable));
            playerManager.AnimationManager.SetBool("Walking", true);
            effect = JoltMoveRuntime.Effect(playerManager, JoltEffect.LightningBlitz);
            // Commit energy now, not after surviving the entire rush.
            wasMoveSuccessful = true;
        }
        base.Finish();
    }

    private void EndDash(bool reward)
    {
        if (!dashing) return;
        dashing = false;
        actionLock.Release();
        JoltMoveRuntime.EndEffect(playerManager, ref effect);
        playerManager.Pokemon.RemoveStatusEffectWithID(JoltMoveRules.BlitzUnstoppable);
        nextHit.Clear();
        if (!reward || playerManager.PlayerState == PlayerState.Dead || playerManager.Pokemon.CurrentHp <= 0) return;
        JoltMoveRuntime.RefreshStat(playerManager.Pokemon, new StatChange(80, Stat.Speed, 6f, true, true, true, JoltMoveRules.OverchargeSpeed));
        playerManager.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(playerManager.Pokemon.GetMaxHp() * 0.2f), 135, 0, 6f, true));
        (playerManager.PassiveController.Passive as JoltPassive)?.UniteFillCharge(5f);
    }

    public override void Cancel() { if (IsActive) Aim.Instance.HideDashAim(); base.Cancel(); }
    public override void ResetMove() { Cancel(); EndDash(false); }
}
