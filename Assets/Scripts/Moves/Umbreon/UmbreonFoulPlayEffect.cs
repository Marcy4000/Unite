using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UmbreonFoulPlayEffect : UmbreonMoveEffect
{
    private bool offered, recast;
    private Vector3 secondDirection;
    private int enemyAttack, ownAttack;
    public override bool TryRecast(Vector3 direction)
    {
        if (!offered || recast || CastInterrupted) return false;
        offered = false; recast = true; secondDirection = direction;
        Actor.MovesController.UmbreonFoulWindowRPC(0);
        return true;
    }
    protected override IEnumerator Execute()
    {
        var victims = new HashSet<Pokemon>();
        yield return Dash(Direction, UmbreonMoveRules.DashDistance, false, victims);
        if (CastInterrupted || victims.Count == 0) yield break;
        offered = true;
        Actor.MovesController.UmbreonFoulWindowRPC(NetworkManager.ServerTime.Time + UmbreonMoveRules.RecastWindow);
        OfferRecast();
        float until = Time.time + UmbreonMoveRules.RecastWindow;
        while (Time.time < until && !recast && UmbreonMoveRules.Living(Actor.Pokemon)) yield return null;
        offered = false;
        if (!recast || !UmbreonMoveRules.Living(Actor.Pokemon)) yield break;
        yield return Dash(secondDirection, UmbreonMoveRules.SecondDashDistance, true, new HashSet<Pokemon>());
    }
    private IEnumerator Dash(Vector3 direction, float length, bool second, HashSet<Pokemon> victims)
    {
        LockActions(); FaceRPC(direction);
        PlayAnimation(UmbreonMoveRules.Animation(Actor, second ? "spell1b22" : "spell1b12"), UmbreonMoveRules.DashDuration);
        if (Upgraded) Combat?.Ready();
        Vector3 destination = Actor.PlayerMovement.GetWallCrossingDestination(Actor.transform.position + direction * length);
        Vector3 previous = Actor.transform.position;
        Drive(destination, UmbreonMoveRules.DashDuration);
        float until = Time.time + UmbreonMoveRules.DashDuration;
        while (!CastInterrupted)
        {
            Physics.SyncTransforms();
            foreach (var target in CombatTargeting.SweptSphereTargets(previous + Vector3.up * 0.8f, Actor.transform.position + Vector3.up * 0.8f, UmbreonMoveRules.DashRadius))
                HitVictim(target, second, direction, victims);
            previous = Actor.transform.position;
            if (Time.time >= until) break;
            yield return null;
        }
        if (second && !CastInterrupted)
            foreach (var target in CombatTargeting.SectorTargets(Actor.transform.position, direction, UmbreonMoveRules.TailRadius, UmbreonMoveRules.TailAngle))
                HitVictim(target, true, direction, victims);
        Visual("Claw", Actor.transform.position + direction, UmbreonMoveRules.TailRadius, 0.25f);
        UnlockActions();
    }
    private void HitVictim(Pokemon target, bool second, Vector3 direction, HashSet<Pokemon> victims)
    {
        if (!CanHit(target) || !victims.Add(target)) return;
        if (!second)
        {
            if (victims.Count == 1) ownAttack = Actor.Pokemon.GetAttack();
            enemyAttack = Mathf.Max(enemyAttack, target.GetAttack());
            target.ResolveDamage(UmbreonMoveRules.Foul(Actor.NetworkObjectId)); Slow(target, 15, 2.5f);
        }
        else
        {
            int amount = UmbreonMoveRules.FoulFollowup(enemyAttack, ownAttack, Actor.Pokemon.GetAttack());
            target.ResolveDamage(DamageInfo.Tuned("umbreon.umbreonfoulplayeffect.hitvictim", new DamageInfo(Actor.NetworkObjectId, 0, 0, (short)Mathf.Min(short.MaxValue, amount), DamageType.Physical)));
            Slow(target, 30, 2.5f);
            if (Stun(target, UmbreonMoveRules.ShoveDuration))
            {
                if (target.TryGetComponent(out PlayerMovement movement)) movement.DisplaceRPC(direction, UmbreonMoveRules.ShoveDistance, UmbreonMoveRules.ShoveDuration);
                else if (target.TryGetComponent(out WildPokemonAI wild)) wild.TryDisplace(direction, UmbreonMoveRules.ShoveDistance, UmbreonMoveRules.ShoveDuration);
            }
        }
    }
    protected override void Cleanup()
    {
        if (IsServer && Actor != null && Actor.IsSpawned) Actor.MovesController.UmbreonFoulWindowRPC(0);
        base.Cleanup();
    }
}
