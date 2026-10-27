using System;
using System.Collections.Generic;

/// <summary>A disposable action lock shared by server effects and melee attacks.</summary>
public sealed class CombatActionLock : IDisposable
{
    public static readonly StatusType[] Interruptions = { StatusType.Incapacitated, StatusType.Asleep, StatusType.Frozen, StatusType.Bound, StatusType.Immobilized };
    private PlayerManager actor;
    private readonly List<IDisposable> leases = new List<IDisposable>();
    private bool movement, server;

    public CombatActionLock(PlayerManager actor, bool lockMovement, bool lockServer = true, bool lockMoves = true)
    {
        this.actor = actor;
        if (actor.IsServer && lockServer) { actor.MovesController.ServerActionLocks++; server = true; }
        if (!actor.IsOwner) return;
        if (lockMoves)
            for (int i = 0; i < 3; i++) leases.Add(actor.MovesController.AcquireMoveStatus(i, ActionStatusType.Busy));
        leases.Add(actor.MovesController.BasicAttackStatus.AcquireStatus(ActionStatusType.Busy));
        leases.Add(actor.MovesController.BattleItemStatus.AcquireStatus(ActionStatusType.Busy));
        leases.Add(actor.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
        leases.Add(actor.RecallStatus.AcquireStatus(ActionStatusType.Busy));
        if (lockMovement) { actor.PlayerMovement.AddMovementRestriction(); movement = true; }
    }

    public void Dispose()
    {
        if (actor == null) return;
        foreach (var lease in leases) lease.Dispose(); leases.Clear();
        if (server) actor.MovesController.ServerActionLocks = Math.Max(0, actor.MovesController.ServerActionLocks - 1);
        if (movement)
        {
            actor.PlayerMovement.RemoveMovementRestriction();
            if (actor.Pokemon.CurrentHp <= 0) actor.PlayerMovement.CanMove = false;
        }
        actor = null;
    }
}
