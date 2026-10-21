using System.Collections.Generic;
using UnityEngine;

public static class JoltMoveRuntime
{
    public static readonly StatusType[] MovementInterrupts =
        { StatusType.Immobilized, StatusType.Incapacitated, StatusType.Asleep, StatusType.Frozen, StatusType.Bound };

    public static bool CanHit(PlayerManager actor, Pokemon target)
    {
        return actor != null && target != null && target.CurrentHp > 0
            && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam)
            && !target.HasStatusEffect(StatusType.Invincible)
            && !target.HasStatusEffect(StatusType.Untargetable);
    }

    public static HashSet<Pokemon> Sweep(PlayerManager actor, Vector3 from, Vector3 to, float radius)
    {
        var targets = new HashSet<Pokemon>();
        foreach (Collider collider in Physics.OverlapCapsule(from + Vector3.up * 0.7f, to + Vector3.up * 0.7f, radius))
            if (collider.TryGetComponent(out Pokemon target) && CanHit(actor, target)) targets.Add(target);
        return targets;
    }

    public static void RefreshStat(Pokemon pokemon, StatChange change)
    {
        pokemon.RemoveAllStatChangeWithIDRPC(change.ID);
        pokemon.AddStatChange(change);
    }

    public static int Effect(PlayerManager actor, JoltEffect effect, bool empowered = false, bool upgraded = false, bool second = false)
    {
        int cast = JoltMoveRules.NextCast();
        actor.MovesController.SpawnJolteonEffectRPC(effect, cast, actor.transform.position, actor.transform.forward, empowered, upgraded, second);
        return cast;
    }

    public static void EndEffect(PlayerManager actor, ref int cast)
    {
        if (cast == 0) return;
        if (actor != null && actor.IsSpawned) actor.MovesController.EndJolteonEffectRPC(cast);
        cast = 0;
    }
}

// Own exactly one movement restriction and one action lock. Cleanup is idempotent.
public sealed class JoltActionLock
{
    private PlayerManager actor;
    public bool Held => actor != null;
    public void Acquire(PlayerManager owner)
    {
        if (Held) return;
        actor = owner;
        actor.PlayerMovement.AddMovementRestriction();
        for (int i = 0; i < 3; i++) actor.MovesController.AddMoveStatus(i, ActionStatusType.Disabled);
        actor.MovesController.BasicAttackStatus.AddStatus(ActionStatusType.Disabled);
        actor.MovesController.BattleItemStatus.AddStatus(ActionStatusType.Disabled);
        actor.ScoreStatus.AddStatus(ActionStatusType.Busy);
    }
    public void Release()
    {
        if (!Held) return;
        actor.PlayerMovement.RemoveMovementRestriction();
        for (int i = 0; i < 3; i++) actor.MovesController.RemoveMoveStatus(i, ActionStatusType.Disabled);
        actor.MovesController.BasicAttackStatus.RemoveStatus(ActionStatusType.Disabled);
        actor.MovesController.BattleItemStatus.RemoveStatus(ActionStatusType.Disabled);
        actor.ScoreStatus.RemoveStatus(ActionStatusType.Busy);
        actor = null;
    }
}
